// Assets/Editor/CutsceneExporter.cs
// CutsceneData(SO) → CSV 4종 익스포터.
// 메뉴: Tools/Cutscene/Export SO → CSV
// 출력: Assets/Datas/CutScene/Csv/{cutscenes,frames,texts,move_images}.csv
//
// - 런타임 코드는 전혀 건드리지 않음. 에디터 전용.
// - 스프라이트는 낱장이면 "경로.png", 아틀라스 슬라이스면 "경로.png[서브이름]" 으로 출력.
// - frames.csv 는 프레임마다 항상 한 줄(빈 프레임 보존). 이 파일이 프레임 개수의 기준.
// - 숫자는 InvariantCulture, bool 은 TRUE/FALSE, enum 은 이름 문자열.
// - 대사 줄바꿈은 \n 리터럴로 박제, 쉼표/따옴표는 CSV 규칙으로 이스케이프.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CutsceneExporter
{
    // SO 검색 폴더 (스크린샷 기준). 폴더가 없으면 프로젝트 전체에서 t:CutsceneData 검색.
    private const string SourceFolder = "Assets/Datas/CutScene/SO";
    private const string OutputDir = "Assets/Datas/CutScene/Csv";

    [MenuItem("Tools/Cutscene/Export SO \u2192 CSV")]
    public static void Export()
    {
        string[] guids = AssetDatabase.IsValidFolder(SourceFolder)
            ? AssetDatabase.FindAssets("t:CutsceneData", new[] { SourceFolder })
            : AssetDatabase.FindAssets("t:CutsceneData");

        var datas = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new { path = p, data = AssetDatabase.LoadAssetAtPath<CutsceneData>(p) })
            .Where(x => x.data != null)
            .ToList();

        if (datas.Count == 0)
        {
            Debug.LogWarning("[CutsceneExporter] CutsceneData \uc790\uc0b0\uc744 \ucc3e\uc9c0 \ubabb\ud588\uc2b5\ub2c8\ub2e4.");
            return;
        }

        var cutscenes = new StringBuilder();
        var frames = new StringBuilder();
        var texts = new StringBuilder();
        var moves = new StringBuilder();

        cutscenes.AppendLine("cutscene_id,kind,stage,asset_path");
        frames.AppendLine("cutscene_id,frame,item_sprite,item_use_fade,item_fade_delay,item_fade_in,item_fade_out,bgm");
        texts.AppendLine("cutscene_id,frame,slot,dialogue,pos_x,pos_y,align,use_fade,fade_delay,fade_in,fade_out");
        moves.AppendLine("cutscene_id,frame,slot,sprite,size_x,size_y,start_x,start_y,end_x,end_y,duration,ease,use_fade,fade_delay,fade_in,fade_out");

        foreach (var entry in datas)
        {
            string id = Path.GetFileNameWithoutExtension(entry.path);
            string kind = GuessKind(id);
            string stage = GuessStage(id);

            cutscenes.AppendLine(Row(id, kind, stage, entry.path));

            CutsceneFrame[] frameArr = entry.data.frames ?? Array.Empty<CutsceneFrame>();
            for (int fi = 0; fi < frameArr.Length; fi++)
            {
                CutsceneFrame frame = frameArr[fi];

                // --- BGM 변경 (변경 없는 프레임은 빈칸) ---
                string bgm = frame.bgmChange != null && frame.bgmChange.changeBGM ? frame.bgmChange.bgm.ToString() : "";

                // --- 아이템 이미지 (프레임당 0~1개, frames.csv 에 인라인) ---
                ItemImageData item = frame.itemImage;
                if (item != null && item.sprite != null)
                {
                    frames.AppendLine(Row(
                        id, fi.ToString(),
                        SpriteToken(item.sprite),
                        B(item.fadeSettings.useFade), F(item.fadeSettings.startDelay),
                        F(item.fadeSettings.fadeInDuration), F(item.fadeSettings.fadeOutDuration),
                        bgm));
                }
                else
                {
                    // 아이템이 없어도 프레임 존재는 한 줄로 보존 (item_sprite 빈칸)
                    frames.AppendLine(Row(id, fi.ToString(), "", "", "", "", "", bgm));
                }

                // --- 텍스트 ---
                List<TextData> ts = frame.Texts ?? new List<TextData>();
                for (int si = 0; si < ts.Count; si++)
                {
                    TextData t = ts[si];
                    texts.AppendLine(Row(
                        id, fi.ToString(), si.ToString(),
                        t.dialogueText ?? "",
                        F(t.textPos.x), F(t.textPos.y),
                        t.textAlignment.ToString(),
                        B(t.fadeSettings.useFade), F(t.fadeSettings.startDelay),
                        F(t.fadeSettings.fadeInDuration), F(t.fadeSettings.fadeOutDuration)));
                }

                // --- 이동 이미지 ---
                List<MoveImageData> ms = frame.moveImages ?? new List<MoveImageData>();
                for (int si = 0; si < ms.Count; si++)
                {
                    MoveImageData m = ms[si];
                    moves.AppendLine(Row(
                        id, fi.ToString(), si.ToString(),
                        m.sprite != null ? SpriteToken(m.sprite) : "",
                        F(m.size.x), F(m.size.y),
                        F(m.startPos.x), F(m.startPos.y),
                        F(m.endPos.x), F(m.endPos.y),
                        F(m.duration), m.ease.ToString(),
                        B(m.fadeSettings.useFade), F(m.fadeSettings.startDelay),
                        F(m.fadeSettings.fadeInDuration), F(m.fadeSettings.fadeOutDuration)));
                }
            }
        }

        Directory.CreateDirectory(OutputDir);
        var enc = new UTF8Encoding(true); // BOM: 엑셀 더블클릭 시 한글 깨짐 방지
        File.WriteAllText(Path.Combine(OutputDir, "cutscenes.csv"), cutscenes.ToString(), enc);
        File.WriteAllText(Path.Combine(OutputDir, "frames.csv"), frames.ToString(), enc);
        File.WriteAllText(Path.Combine(OutputDir, "texts.csv"), texts.ToString(), enc);
        File.WriteAllText(Path.Combine(OutputDir, "move_images.csv"), moves.ToString(), enc);

        AssetDatabase.Refresh();
        Debug.Log($"[CutsceneExporter] \uc644\ub8cc: {datas.Count}\uac1c \ucef7\uc52c \u2192 {OutputDir}");
    }

    // ---------- helpers ----------

    private static string Row(params string[] cells)
        => string.Join(",", cells.Select(Csv));

    private static string Csv(string s)
    {
        if (s == null) s = "";
        // 줄바꿈은 \n 리터럴로 박제 (한 레코드 = 한 줄 유지)
        s = s.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        bool needQuote = s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0;
        if (s.IndexOf('"') >= 0) s = s.Replace("\"", "\"\"");
        return needQuote ? "\"" + s + "\"" : s;
    }

    private static string F(float v) => v.ToString(CultureInfo.InvariantCulture);
    private static string B(bool v) => v ? "TRUE" : "FALSE";

    // 낱장 → "경로", 아틀라스 슬라이스(같은 파일에 Sprite 2개 이상) → "경로[서브이름]"
    private static string SpriteToken(Sprite sprite)
    {
        string p = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(p)) return sprite.name; // 안전망

        int spriteCount = AssetDatabase.LoadAllAssetsAtPath(p).Count(o => o is Sprite);
        return spriteCount > 1 ? $"{p}[{sprite.name}]" : p;
    }

    private static string GuessKind(string id)
        => id.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 ? "item" : "stage";

    // 파일명 앞쪽 숫자 추출 (예: "01_ItemCutScene" → "01"). 추정값이므로 시트에서 보정 가능.
    private static string GuessStage(string id)
        => new string(id.TakeWhile(char.IsDigit).ToArray());
}
#endif