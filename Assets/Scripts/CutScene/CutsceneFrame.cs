using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
// using System.Drawing;

[System.Serializable]
public class MoveImageData
{
    public Sprite sprite;
    public Vector2 size; // 이미지 크기
    public FaidInOut fadeSettings;

    [Header("움직임 (필요없으면 duration = 0")]
    public Vector2 startPos;
    public Vector2 endPos;
    public float duration;
    public Ease ease;
}

[System.Serializable]
public class ItemImageData
{
    public Sprite sprite;
    public FaidInOut fadeSettings;

    [Header("아이템 배경 위치 (필요없으면 useCustomBGPos = false → 씬 기본 위치)")]
    public bool useCustomBGPos;
    public Vector2 bgPos;
}
[System.Serializable]
public class TextData
{
    [TextArea] public string dialogueText;
    public Vector2 textPos;
    public TextAlignmentOptions textAlignment = TextAlignmentOptions.Left; // 기본값 좌정렬
    public FaidInOut fadeSettings;

}
[System.Serializable]
public class FaidInOut
{
    [Header("페이드 (필요없으면 useFade = false)")]
    public bool useFade;
    public float startDelay; // 페이드 시작 지연
    public float fadeInDuration;
    public float fadeOutDuration;
}
[System.Serializable]
public class SFXData
{
    [Header("효과음 (필요없으면 playSFX = false)")]
    public bool playSFX;
    public SoundManager.SFX sfx;
}
[System.Serializable]
public class CutsceneFrame
{
    [Header("효과음 (이 프레임이 나올 때 재생, BGM과 무관)")]
    public SFXData sfx;

    [Header("아이템 이미지")]
    public ItemImageData itemImage;

    [Header("텍스트")]
    public List<TextData> Texts;

    [Header("요소 이미지 리스트(없을 시 null)")]
    public List<MoveImageData> moveImages;

}
