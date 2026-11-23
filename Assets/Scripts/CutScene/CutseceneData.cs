using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/CutsceneData")]
public class CutsceneData : ScriptableObject
{
    public SoundManager.BGM targetBGM; // 컷씬 전체에서 재생할 BGM (컷씬당 1개)
    public CutsceneFrame[] frames;
}
