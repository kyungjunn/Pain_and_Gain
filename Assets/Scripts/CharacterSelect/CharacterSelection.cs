using System;
using UnityEngine;

// 씬 전환 동안 이번 게임의 확정 선택만 유지한다.
public static class CharacterSelection
{
    public static PlayerCharacterId SelectedId { get; private set; } = PlayerCharacterId.Ninja;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SelectedId = PlayerCharacterId.Ninja;
    }

    public static void Confirm(PlayerCharacterId id)
    {
        if (!Enum.IsDefined(typeof(PlayerCharacterId), id))
            throw new ArgumentOutOfRangeException(nameof(id));

        SelectedId = id;
    }
}
