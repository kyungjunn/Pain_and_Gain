using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ComponentLifecycleValidation
{
    public static string Status { get; private set; } = "Not run";

    public static void Start()
    {
        var game = GameManager.Instance;
        if (!Application.isPlaying || game == null || !game.isActiveAndEnabled ||
            game.IsPaused || Time.timeScale <= 0f || Status == "Running")
            throw new InvalidOperationException("Start in unpaused Play mode with an active GameManager coroutine host.");
        Status = "Running";
        game.StartCoroutine(Step(Run()));
    }

    private static IEnumerator Step(IEnumerator test)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(test);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            bool moved = false;
            object yielded = null;
            Exception failure = null;
            try { moved = current.MoveNext(); if (moved) yielded = current.Current; }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception cleanupError) { failure = new AggregateException(failure, cleanupError); }
                }
                Status = "Failed: " + failure;
                Debug.LogError(Status);
                yield break;
            }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        Status = "Passed";
        Debug.Log("COMPONENT_LIFECYCLE_VALIDATION_PASS");
    }

    private static IEnumerator Run()
    {
        Scene fixtureScene = default;
        Scene unrelatedScene = default;
        Scene managerScene = default;
        var cleanup = new List<AsyncOperation>();
        QuestManager questManager = null;
        try
        {
            // Regular additive scenes run real Awake/OnEnable callbacks. All actors are
            // untagged and isolated; no real player, singleton or session is replaced.
            string suffix = Guid.NewGuid().ToString("N");
            fixtureScene = SceneManager.CreateScene("LifecycleFixtures_" + suffix);
            unrelatedScene = SceneManager.CreateScene("LifecycleUnrelated_" + suffix);
            managerScene = SceneManager.CreateScene("LifecycleManager_" + suffix);
            var player = CreateInactive("LifecyclePlayer", fixtureScene, new Vector3(10000f, 1000f, 10000f));
            var stats = player.AddComponent<PlayerStats>();
            stats.baseHP = 100f;
            stats.baseDefense = 0f;
            stats.ApplyStat(AugmentType.HP, 100f);

            // PlayerHealth adds feedback in Awake. Supply a local camera/listener so that
            // feedback initialization never installs a listener on the real Camera.main.
            var camera = player.AddComponent<CinemachineCamera>();
            camera.enabled = false;
            player.AddComponent<CinemachineImpulseListener>();
            var feedback = player.AddComponent<PlayerCombatFeedback>();
            feedback.enabled = false;
            var numbers = player.AddComponent<CombatDamageNumbers>();
            numbers.enabled = false;
            var health = player.AddComponent<PlayerHealth>();
            player.GetComponent<Rigidbody>().isKinematic = true;
            player.SetActive(true);
            Require(health.CurrentHealth == 200 && health.MaxHealth == 200, "health fixture initializes from augmented HP");
            health.TakeDamage(20);
            Require(health.CurrentHealth == 180, "health fixture has missing HP before disable");
            health.enabled = false;
            Require(Mathf.Approximately(stats.ReduceStatByRatio(AugmentType.HP, 1f), 100f), "HP penalty removes the fixture augment");
            Require(health.CurrentHealth == 180 && health.MaxHealth == 100, "disabled health has not handled the stats event");
            health.enabled = true;
            Require(health.CurrentHealth == 100, "re-enabled health clamps HP to the reduced maximum");
            health.TakeDamage(25);
            health.enabled = false;
            stats.ApplyStat(AugmentType.HP, 100f);
            health.enabled = true;
            Require(health.CurrentHealth == 75 && health.MaxHealth == 200, "re-enable and maximum increase never heal missing HP");
            health.enabled = false;
            stats.ReduceStatByRatio(AugmentType.HP, 1f);
            health.enabled = true;
            Require(health.CurrentHealth == 75 && health.MaxHealth == 100, "clamping below the new maximum does not heal");

            ValidateMovement(fixtureScene);
            ValidateChannel(fixtureScene);
            yield return ValidateAttack(fixtureScene);
            ValidatePursuit(fixtureScene);

            // Never activate this manager: Awake/OnEnable would claim the singleton or
            // bind the real SpawnManager player. Exercise its callbacks directly instead.
            var questObject = CreateInactive("LifecycleQuestManager", managerScene, Vector3.zero);
            questManager = questObject.AddComponent<QuestManager>();
            ValidateQuestSceneOwnership(questManager, player, fixtureScene, unrelatedScene);
            Debug.Log("COMPONENT_LIFECYCLE_SKIP: actual NavMesh movement/agent-stop assertion (isolated agent is disabled).");
        }
        finally
        {
            try
            {
                if (questManager != null)
                    questManager.ClearSession();
            }
            finally
            {
                try { UnloadFixtureScene(fixtureScene, cleanup); }
                finally
                {
                    try { UnloadFixtureScene(unrelatedScene, cleanup); }
                    finally { UnloadFixtureScene(managerScene, cleanup); }
                }
            }
        }
        foreach (var operation in cleanup)
            if (operation != null) yield return operation;
    }

    private static void UnloadFixtureScene(Scene scene, List<AsyncOperation> cleanup)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
        cleanup.Add(SceneManager.UnloadSceneAsync(scene));
    }

    private static void ValidateMovement(Scene scene)
    {
        Vector3 origin = new Vector3(13000f, 1000f, 13000f);
        var floor = CreateInactive("LifecycleGround", scene, origin + Vector3.down);
        var floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.size = new Vector3(2f, 0.2f, 2f);
        floor.SetActive(true);
        var actor = CreateInactive("LifecycleMovement", scene, origin);
        var movement = actor.AddComponent<PlayerMovement>();
        movement.cameraTransform = actor.transform;
        movement.moveSpeed = 6f;
        var body = actor.GetComponent<Rigidbody>();
        body.useGravity = false;
        actor.SetActive(true);
        body.linearVelocity = new Vector3(0f, 2f, 0f);
        Vector3 before = actor.transform.position;
        movement.Move(new Vector2(0f, 0.5f));
        Require(actor.transform.position == before && body.linearVelocity == new Vector3(0f, 2f, 0f),
            "Move queues input without moving the transform or writing velocity");
        Invoke(movement, "FixedUpdate");
        Require(Mathf.Abs(body.linearVelocity.z - 3f) < 0.001f &&
            Mathf.Abs(body.linearVelocity.x) < 0.001f && Mathf.Abs(body.linearVelocity.y - 2f) < 0.001f,
            "FixedUpdate preserves half-stick speed and vertical velocity");
        movement.StopMovement();
        Require(body.linearVelocity == new Vector3(0f, 2f, 0f), "StopMovement clears only owned horizontal velocity");
        Physics.SyncTransforms();
        Require(movement.CheckGrounded(), "jump fixture has a small isolated ground collider");
        Require(movement.Jump() && !movement.Jump(), "jump accepts only one reservation before the physics step");
        Require(body.linearVelocity == new Vector3(0f, 2f, 0f), "queued jump applies no immediate impulse");
        movement.StopMovement();
        Invoke(movement, "FixedUpdate");
        Require(body.linearVelocity == new Vector3(0f, 2f, 0f), "StopMovement cancels the queued jump without changing vertical velocity");
        actor.SetActive(false);
        floor.SetActive(false);
    }

    private static void ValidateChannel(Scene scene)
    {
        NinjaHwaDunSkillSO skill = null;
        try
        {
            var actor = CreateInactive("LifecycleChannelOwner", scene, new Vector3(14000f, 1000f, 14000f));
            actor.AddComponent<PlayerStateManager>();
            actor.AddComponent<PlayerDamageDealer>();
            var owner = actor.AddComponent<PlayerSkillController>();
            actor.SetActive(true);
            skill = ScriptableObject.CreateInstance<NinjaHwaDunSkillSO>();
            var effect = CreateInactive("LifecycleChannel", scene, actor.transform.position);
            var collider = effect.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var channel = effect.AddComponent<NinjaHwaDunChannel>();
            effect.SetActive(true);
            Require(channel.Initialize(owner, skill, 1, 0.1f, 5f), "isolated channel accepts an active owner");
            Require(channel.enabled && collider.enabled, "initialized channel and collider are enabled");
            owner.enabled = false;
            Require(!channel.enabled && !collider.enabled, "disabling the skill controller synchronously cancels its channel and collider");
            owner.enabled = true;
            Require(!channel.Initialize(owner, skill, 1, 0.1f, 5f), "re-enable cannot revive a stopped channel");
            actor.SetActive(false);
        }
        finally
        {
            if (skill != null) Object.Destroy(skill);
        }
    }

    private static IEnumerator ValidateAttack(Scene scene)
    {
        Vector3 origin = new Vector3(11000f, 1000f, 11000f);
        var victim = CreateInactive("LifecycleAttackVictim", scene, origin + Vector3.forward);
        var health = victim.AddComponent<EnemyHealth>();
        Set(health, "maxHealth", 1000);
        Set(health, "expReward", 0);
        victim.SetActive(true);
        var attacker = CreateInactive("LifecycleAttacker", scene, origin);
        var attack = attacker.AddComponent<EnemyAttack>();
        Set(attack, "attackDamage", 10);
        Set(attack, "attackCooldown", 0f);
        Set(attack, "fallbackHitDelay", 0.05f);
        attacker.SetActive(true);
        int before = health.CurrentHealth;
        Require(attack.TryAttack(victim.transform), "enabled attack accepts an in-range target");
        attack.enabled = false;
        Require(!attack.TryAttack(victim.transform), "disabled TryAttack rejects even with zero cooldown");
        attack.ApplyAttackDamageFromAnimationEvent();
        Require(health.CurrentHealth == before, "animation hit while disabled deals no damage");
        // Re-enable before the delay elapses: checking only while disabled would miss
        // stale pending damage that becomes valid again on re-enable.
        attack.enabled = true;
        attack.ApplyAttackDamageFromAnimationEvent();
        Require(health.CurrentHealth == before, "re-enable does not revive the pending animation hit");
        double deadline = Time.unscaledTimeAsDouble + 3f;
        double started = Time.timeAsDouble;
        while (Time.timeAsDouble - started < 0.12)
        {
            if (Time.unscaledTimeAsDouble >= deadline)
                throw new InvalidOperationException("Attack fallback test requires advancing scaled time; resume the session before running.");
            yield return null;
        }
        Require(health.CurrentHealth == before, "cancelled fallback cannot apply delayed damage after re-enable");
        Require(attack.TryAttack(victim.transform), "re-enabled attack can start a fresh cycle");
        attack.ApplyAttackDamageFromAnimationEvent();
        Require(health.CurrentHealth == before - 10, "fresh attack deals damage through IDamageable");
        attack.ApplyAttackDamageFromAnimationEvent();
        Require(health.CurrentHealth == before - 10, "fresh attack applies damage only once");
        attacker.SetActive(false);
        victim.SetActive(false);
    }

    private static void ValidatePursuit(Scene scene)
    {
        const float range = 10f;
        Vector3 origin = new Vector3(12000f, 1000f, 12000f);
        var target = CreateInactive("LifecyclePursuitTarget", scene, origin + Vector3.forward * 5f);
        target.SetActive(true);
        var enemy = CreateInactive("LifecycleAI", scene, origin);
        // Add and disable the agent before activation: these are deterministic state
        // tests, not a dependency on the current gameplay scene's baked NavMesh.
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        var health = enemy.AddComponent<EnemyHealth>();
        Set(health, "expReward", 0);
        var ai = enemy.AddComponent<EnemyAI>();
        Set(ai, "detectionRange", range);
        Set(ai, "playerTag", "");
        enemy.SetActive(true);
        ai.SetTarget(target.transform);
        Tick(ai);
        Require(ai.CurrentState == EnemyAI.EnemyState.Chase, "target inside detection starts pursuit");
        enemy.transform.position = origin + Vector3.forward * (range * 3f);
        target.transform.position = enemy.transform.position + Vector3.forward * 5f;
        Tick(ai);
        Require(ai.CurrentState == EnemyAI.EnemyState.Chase, "distance from pursuit origin never suppresses an in-range target");
        for (int cycle = 0; cycle < 3; cycle++)
        {
            target.transform.position = enemy.transform.position + Vector3.forward * (range + 0.1f);
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Patrol, "leaving detection returns to patrol every time");
            target.transform.position = enemy.transform.position + Vector3.forward * range;
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Chase, "re-entering detection immediately resumes pursuit every time");
            target.transform.position = enemy.transform.position + Vector3.forward;
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Attack, "nearby target enters attack even during attack cooldown");
            target.transform.position = enemy.transform.position + Vector3.forward * 5f;
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Chase, "leaving attack range while inside detection resumes pursuit");
        }
        ai.enabled = false;
        enemy.SetActive(false);
        target.SetActive(false);
    }

    private static void ValidateQuestSceneOwnership(QuestManager manager, GameObject player, Scene owner, Scene unrelated)
    {
        QuestSO quest = null;
        try
        {
            Invoke(manager, "HandlePlayerSpawned", player);
            quest = ScriptableObject.CreateInstance<QuestSO>();
            SetProperty(manager, "State", QuestState.Active);
            SetProperty(manager, "CurrentQuest", quest);
            SetProperty(manager, "CurrentProgress", 3);
            SetProperty(manager, "RemainingTime", 19f);
            Invoke(manager, "HandleSceneUnloaded", unrelated);
            Require(manager.State == QuestState.Active && manager.CurrentQuest == quest &&
                manager.CurrentProgress == 3 && Mathf.Approximately(manager.RemainingTime, 19f),
                "unrelated unload preserves quest state, quest, progress and timer");
            Require((GameObject)Get(manager, "player") == player, "unrelated unload preserves the bound player");
            Invoke(manager, "HandleSceneUnloaded", owner);
            Require(manager.State == QuestState.Stopped && manager.CurrentQuest == null &&
                manager.CurrentProgress == 0 && manager.RemainingTime == 0f,
                "owning scene unload clears quest state");
            Require((GameObject)Get(manager, "player") == null, "owning scene unload clears the player binding");
            Invoke(manager, "HandlePlayerSpawned", player);
            SetProperty(manager, "State", QuestState.Active);
            SetProperty(manager, "CurrentQuest", quest);
            Invoke(manager, "HandleSceneUnloaded", manager.gameObject.scene);
            Require(manager.State == QuestState.Stopped && manager.CurrentQuest == null &&
                (GameObject)Get(manager, "player") == null,
                "manager scene unload also clears its owned quest session");
        }
        finally
        {
            manager.ClearSession();
            if (quest != null)
                Object.Destroy(quest);
        }
    }

    public static string ValidateNavigation()
    {
        var game = GameManager.Instance;
        if (!Application.isPlaying || game == null || game.CurrentPlayer == null)
            throw new InvalidOperationException("Navigation validation requires a spawned player and loaded NavMesh.");
        Require(NavMesh.SamplePosition(game.CurrentPlayer.transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas),
            "live map provides a NavMesh fixture position");
        var actor = CreateInactive("LifecycleNavigation", game.CurrentPlayer.scene, hit.position);
        var target = CreateInactive("LifecycleNavigationTarget", game.CurrentPlayer.scene, hit.position + Vector3.forward * 5f);
        try
        {
            var ai = actor.AddComponent<EnemyAI>();
            var agent = actor.GetComponent<NavMeshAgent>();
            actor.SetActive(true);
            target.SetActive(true);
            ai.SetTarget(target.transform);
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Chase && !agent.isStopped,
                "live agent begins pursuit");
            ai.enabled = false;
            Require(agent.isStopped && !agent.hasPath && !agent.pathPending && agent.velocity == Vector3.zero,
                "disable clears even pending navigation and leaves the agent stopped");
            ai.enabled = true;
            ai.SetTarget(target.transform);
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Chase && !agent.isStopped,
                "re-enabled live agent can pursue again");
            target.transform.position = hit.position + Vector3.forward * 100f;
            Tick(ai);
            Require(ai.CurrentState == EnemyAI.EnemyState.Patrol,
                "live agent returns to patrol when the player leaves detection");
            Require(!agent.hasPath || (agent.destination - hit.position).sqrMagnitude < 100f,
                "patrol does not retain the old pursuit path");
            return "Passed";
        }
        finally
        {
            actor.SetActive(false);
            Object.Destroy(actor);
            Object.Destroy(target);
        }
    }

    private static GameObject CreateInactive(string name, Scene scene, Vector3 position)
    {
        var fixture = new GameObject(name);
        fixture.SetActive(false);
        SceneManager.MoveGameObjectToScene(fixture, scene);
        fixture.transform.position = position;
        return fixture;
    }

    private static void Tick(EnemyAI ai) => Invoke(ai, "Update");
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetSetMethod(true).Invoke(target, new[] { value });
    private static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("COMPONENT_LIFECYCLE_FAIL: " + message);
        Debug.Log("COMPONENT_LIFECYCLE_PASS: " + message);
    }
}
