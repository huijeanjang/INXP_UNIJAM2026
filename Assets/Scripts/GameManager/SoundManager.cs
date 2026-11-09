using UnityEngine;
using UnityEngine.Audio;
public class SoundManager : MonoBehaviour
{
    [Header("Sound Players")]
    public AudioSource bgmPlayer;                       //배경음 재생하기 위한 오디오소스
    public AudioSource[] sfxPlayers = new AudioSource[10]; // 효과음을 재생하기 위한 오디오소스 (동시재생을 위해 배열로 구현)

    private float[] sfxPlayStartTime = new float[10];  // 효과음 재생 시간 기록
    public GameObject soundmanager;

    [Header("Audio Clips")]
    public AudioClip[] bgmSound;                //재생할 BGM 저장소
    public AudioClip[] sfxSounds;               //재생할 SFX 저장소

    [Header("Audio Mixer")]  // ← 새로 추가!
    public AudioMixerGroup bgmMixerGroup;   // BGM Mixer Group
    public AudioMixerGroup sfxMixerGroup;   // SFX Mixer Group

    public enum SFX
    {
        ButtonClick = 0, // UI 버튼 클릭
        ThreeMatch = 1, // 3개 매치 성공
        AddScore = 2, // 점수 획득
        GetItem = 3, // 아이템 획득
        Die = 4, // 플러피 버드 중 사망
        Cassette = 5, // 카세트 테이프 (현재->과거)

    }

    public enum BGM
    {
        Title = 0, // 타이틀 BGM
        Anipang = 1, // 애니팡 BGM
        FlappyBird = 2, // 플래피버드 BGM
        Prologue = 3, // 프롤로그 BGM
        Cutscene = 4, // 컷씬 BGM
    }
    private void Awake()
    {
        // soundmanager를 현재 게임오브젝트로 할당
        if (soundmanager == null)
        {
            soundmanager = gameObject;
        }

        // BGM Player Mixer 연결
        if (bgmPlayer == null)
        {
            bgmPlayer = gameObject.AddComponent<AudioSource>();
        }
        bgmPlayer.outputAudioMixerGroup = bgmMixerGroup;

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i] = soundmanager.AddComponent<AudioSource>();
            sfxPlayers[i].outputAudioMixerGroup = sfxMixerGroup;
            sfxPlayStartTime[i] = -1f;  // 초기값 (사용 안 함)
        }
    }
    private void Start()
    {
        Init();
        PlayBGM(BGM.Title); //타이틀 BGM 재생
    }
    // BGM
    public void PlayBGM(BGM bgm)
    {
        PlayBGM(bgmSound[(int)bgm]);
    }

    public void PlayBGM(AudioClip bgmclip = null)
    {
        if (bgmclip != null)
        {
            bgmPlayer.clip = bgmclip;
        }
        bgmPlayer.Play();
    }

    public void StopBGM() => bgmPlayer.Stop();
    public void PauseBGM() => bgmPlayer.Pause();
    public void UnPauseBGM() => bgmPlayer.UnPause();

    // SFX
    public void PlaySFX(SFX sfx)
    {
        PlaySFX((int)sfx);
    }

    public void PlaySFX(AudioClip sfxSound)
    {
        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            if (sfxPlayers[index] == null || sfxPlayers[index].isPlaying) continue;

            sfxPlayers[index].clip = sfxSound;
            sfxPlayers[index].Play();
            return;
        }
        Debug.LogWarning("All SFX players are busy!");
    }

    public void PlaySFX(int sfxSound)
    {
        if (sfxSound < 0 || sfxSound >= sfxSounds.Length)
        {
            Debug.LogError($"Invalid SFX index: {sfxSound}");
            return;
        }

        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            if (sfxPlayers[index] == null || sfxPlayers[index].isPlaying) continue;

            sfxPlayers[index].clip = sfxSounds[sfxSound];
            sfxPlayers[index].Play();
            sfxPlayStartTime[index] = Time.time; // 재생 시작 시간 기록
            return;
        }

        // 빈 슬롯 없을 시 가장 오래된 효과음 교체
        int oldestIndex = -1;
        float oldestTime = float.MaxValue;

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            if (sfxPlayers[i] == null) continue;
            if (sfxPlayStartTime[i] < oldestTime)
            {
                oldestTime = sfxPlayStartTime[i];
                oldestIndex = i;
            }
        }

        if (oldestIndex < 0) return;

        sfxPlayers[oldestIndex].Stop();
        sfxPlayers[oldestIndex].clip = sfxSounds[sfxSound];
        sfxPlayers[oldestIndex].Play();
        sfxPlayStartTime[oldestIndex] = Time.time;

    }

    public void Init()
    {
        bgmPlayer.playOnAwake = true;
        bgmPlayer.loop = true;
        bgmPlayer.clip = bgmSound[(int)BGM.Title];

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].playOnAwake = false;
            sfxPlayers[i].loop = false;
        }

        ApplyVolume();
    }

    public void ApplyVolume()
    {
        if (GameManager.Instance?.GameData == null) return;

        bgmPlayer.volume = GameManager.Instance.GameData.backGroundMusicVolume;
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].volume = GameManager.Instance.GameData.effectSoundVolume;
        }
    }
}
