using UnityEngine;

public class ResultCharacterDisplay : MonoBehaviour
{
    [System.Serializable]
    public struct CharacterVisual
    {
        public PlayerCharacterId id;
        public GameObject model;
    }

    [SerializeField] private CharacterVisual[] characters;

    void Start()
    {
        // ↓ CharacterSelection에서 선택된 캐릭터를 꺼내는 이름으로 맞추세요
        PlayerCharacterId selected = CharacterSelection.SelectedId;
        Debug.Log("엔딩 캐릭터: " + selected);

        foreach (var c in characters)
        {
            if (c.model != null)
                c.model.SetActive(c.id == selected);
        }
    }
}