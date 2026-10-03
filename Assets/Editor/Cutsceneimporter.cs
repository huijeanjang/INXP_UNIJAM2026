// Assets/Editor/CutsceneImporter.cs
// CSV 4종 → CutsceneData(SO) 임포터. (CutsceneExporter 의 역방향)
// 메뉴: Tools/Cutscene/Import CSV → SO
// 입력: Assets/Datas/CutScene/Csv/{cutscenes,frames,texts,move_images}.csv
//
// 핵심 규칙(Exporter 와 정확히 대칭):
// - 기존 에셋은 "제자리 갱신"(삭제·재생성 금지) → GUID 보존 → 매니저 참조 안 끊김.
// - 스프라이트 토큰: "경로"  또는  "경로[서브이름]"(아틀라스 슬라이스).
// - 대사의 \n 리터럴 → 실제 줄바꿈 문자로 복원.
// - 숫자는 InvariantCulture, bool 은 TRUE/1, enum 은 이름 또는 숫자.
// - 슬롯은 프레임마다 0부터 연속이어야 함(플레이어가 인덱스로 풀에 매핑하므로).
// - 에러가 하나라도 있으면 아무것도 굽지 않고 전부 로그 후 중단.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

public static class CutsceneImporter
{
    private const string InputDir = "Assets/Datas/CutScene/Csv";
    private const string SoOutputDir = "Assets/Datas/CutScene/SO";

    // 현재 CutscenePlayer 의 풀 크기. 슬롯이 이 값을 넘으면 경고(런타임에서 무시됨).
    private const int MaxMoveSlots = 3;
    private const int MaxTextSlots = 3;

    [MenuItem("Tools/Cutscene/Import CSV \u2192 SO")]
    public static void Import()
    {
        var errors = new List<string>();

        // ---- 1. CSV 로드 ----
        Table cutscenesT = LoadTable("cutscenes.csv", errors);
        Table framesT = LoadTable("frames.csv", errors);
        Table textsT = LoadTable("texts.csv", errors);
        Table movesT = LoadTable("move_images.csv", errors);
        if (errors.Count > 0) { Abort(errors); return; }

        // ---- 2. 컷씬 메타 ----
        var builders = new Dictionary<string, CutsceneBuilder>();
        foreach (var row in cutscenesT.Rows)
        {
            string id = cutscenesT.Get(row, "cutscene_id").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            string path = cutscenesT.Get(row, "asset_path").Trim();

            if (builders.ContainsKey(id)) { errors.Add($"[cutscenes] cutscene_id 중복: {id}"); continue; }
            if (string.IsNullOrEmpty(path)) { errors.Add($"[cutscenes] {id}: asset_path 비어 있음"); continue; }

            string soPath = Path.Combine(SoOutputDir, Path.GetFileName(path));
            builders[id] = new CutsceneBuilder { id = id, assetPath = soPath };
        }
        if (errors.Count > 0) { Abort(errors); return; }

        // ---- 3. 프레임 + 아이템 이미지 ----
        foreach (var row in framesT.Rows)
        {
            string id = framesT.Get(row, "cutscene_id").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!builders.TryGetValue(id, out var b)) { errors.Add($"[frames] 알 수 없는 cutscene_id: {id}"); continue; }

            int frame = ParseInt(framesT.Get(row, "frame"), $"[frames] {id} frame", errors);
            var fb = b.Frame(frame);

            // BGM 변경 (빈칸이면 변경 없음)
            string bgm = framesT.Get(row, "bgm").Trim();
            if (!string.IsNullOrEmpty(bgm))
            {
                fb.bgmChange = new BGMChangeData
                {
                    changeBGM = true,
                    bgm = ParseEnum<SoundManager.BGM>(bgm, default, $"[frames] {id} f{frame} bgm", errors)
                };
            }

            string sprite = framesT.Get(row, "item_sprite").Trim();
            string bgX = framesT.Get(row, "item_bg_x").Trim();
            string bgY = framesT.Get(row, "item_bg_y").Trim();
            bool customBG = !string.IsNullOrEmpty(bgX) || !string.IsNullOrEmpty(bgY); // 빈칸이면 씬 기본 위치
            if (!string.IsNullOrEmpty(sprite) || customBG)
            {
                string ctx = $"[frames] {id} f{frame} item";
                fb.item = new ItemImageData
                {
                    sprite = string.IsNullOrEmpty(sprite) ? null : ResolveSprite(sprite, ctx, errors),
                    fadeSettings = Fade(framesT, row, "item_use_fade", "item_fade_delay", "item_fade_in", "item_fade_out", ctx, errors),
                    useCustomBGPos = customBG,
                    bgPos = customBG ? V2(bgX, bgY, $"{ctx} bg", errors) : Vector2.zero
                };
            }
        }

        // ---- 4. 텍스트 ----
        foreach (var row in textsT.Rows)
        {
            string id = textsT.Get(row, "cutscene_id").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!builders.TryGetValue(id, out var b)) { errors.Add($"[texts] 알 수 없는 cutscene_id: {id}"); continue; }

            int frame = ParseInt(textsT.Get(row, "frame"), $"[texts] {id} frame", errors);
            int slot = ParseInt(textsT.Get(row, "slot"), $"[texts] {id} f{frame} slot", errors);
            string ctx = $"[texts] {id} f{frame} s{slot}";

            var t = new TextData
            {
                dialogueText = Unescape(textsT.Get(row, "dialogue")), // \n 복원, 트림 안 함(의도적 공백 보존)
                textPos = V2(textsT.Get(row, "pos_x"), textsT.Get(row, "pos_y"), ctx, errors),
                textAlignment = ParseEnum<TextAlignmentOptions>(textsT.Get(row, "align").Trim(), TextAlignmentOptions.Left, $"{ctx} align", errors),
                fadeSettings = Fade(textsT, row, "use_fade", "fade_delay", "fade_in", "fade_out", ctx, errors)
            };
            b.Frame(frame).AddText(slot, t, errors, ctx);
        }

        // ---- 5. 이동 이미지 ----
        foreach (var row in movesT.Rows)
        {
            string id = movesT.Get(row, "cutscene_id").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!builders.TryGetValue(id, out var b)) { errors.Add($"[move_images] 알 수 없는 cutscene_id: {id}"); continue; }

            int frame = ParseInt(movesT.Get(row, "frame"), $"[move_images] {id} frame", errors);
            int slot = ParseInt(movesT.Get(row, "slot"), $"[move_images] {id} f{frame} slot", errors);
            string ctx = $"[move_images] {id} f{frame} s{slot}";

            string sprite = movesT.Get(row, "sprite").Trim();
            var m = new MoveImageData
            {
                sprite = string.IsNullOrEmpty(sprite) ? null : ResolveSprite(sprite, ctx, errors),
                size = V2(movesT.Get(row, "size_x"), movesT.Get(row, "size_y"), ctx, errors),
                startPos = V2(movesT.Get(row, "start_x"), movesT.Get(row, "start_y"), ctx, errors),
                endPos = V2(movesT.Get(row, "end_x"), movesT.Get(row, "end_y"), ctx, errors),
                duration = ParseFloat(movesT.Get(row, "duration"), $"{ctx} duration", errors),
                ease = ParseEnum<Ease>(movesT.Get(row, "ease").Trim(), Ease.Unset, $"{ctx} ease", errors),
                fadeSettings = Fade(movesT, row, "use_fade", "fade_delay", "fade_in", "fade_out", ctx, errors)
            };
            b.Frame(frame).AddMove(slot, m, errors, ctx);
        }

        if (errors.Count > 0) { Abort(errors); return; }

        // ---- 6. 슬롯 연속성 / 상한 검증 ----
        foreach (var b in builders.Values)
            b.Validate(errors, MaxMoveSlots, MaxTextSlots);
        if (errors.Count > 0) { Abort(errors); return; }

        // ---- 7. SO 굽기 (제자리 갱신, 에러 0일 때만 여기 도달) ----
        int written = 0, frameCount = 0;
        foreach (var b in builders.Values)
        {
            var data = AssetDatabase.LoadAssetAtPath<CutsceneData>(b.assetPath);
            bool created = false;
            if (data == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(b.assetPath));
                data = ScriptableObject.CreateInstance<CutsceneData>();
                AssetDatabase.CreateAsset(data, b.assetPath);
                created = true;
            }

            data.frames = b.BuildFrames();
            EditorUtility.SetDirty(data);

            written++;
            frameCount += data.frames.Length;
            CustomLogSafe($"[CutsceneImporter] {(created ? "생성" : "갱신")}: {b.id} ({data.frames.Length} frames)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CutsceneImporter] 완료: 컷씬 {written}개 / 프레임 {frameCount}개 구움.");
    }

    // ============================================================
    //  빌더
    // ============================================================
    private class CutsceneBuilder
    {
        public string id;
        public string assetPath;
        private readonly SortedDictionary<int, FrameBuilder> _frames = new SortedDictionary<int, FrameBuilder>();

        public FrameBuilder Frame(int i)
        {
            if (!_frames.TryGetValue(i, out var f)) { f = new FrameBuilder(i); _frames[i] = f; }
            return f;
        }

        public void Validate(List<string> errors, int maxMove, int maxText)
        {
            // 프레임 인덱스가 0부터 연속인지
            int expected = 0;
            foreach (var kv in _frames)
            {
                if (kv.Key != expected)
                    errors.Add($"[{id}] 프레임 번호가 연속이 아님: {expected} 가 비어 있고 {kv.Key} 가 있음");
                expected++;
                kv.Value.Validate(id, errors, maxMove, maxText);
            }
        }

        public CutsceneFrame[] BuildFrames()
        {
            int count = _frames.Count == 0 ? 0 : _frames.Keys.Max() + 1;
            var arr = new CutsceneFrame[count];
            for (int i = 0; i < count; i++)
                arr[i] = _frames.TryGetValue(i, out var f) ? f.Build() : new CutsceneFrame
                {
                    bgmChange = new BGMChangeData(),
                    itemImage = new ItemImageData(),
                    Texts = new List<TextData>(),
                    moveImages = new List<MoveImageData>()
                };
            return arr;
        }
    }

    private class FrameBuilder
    {
        public readonly int index;
        public BGMChangeData bgmChange;
        public ItemImageData item;
        private readonly SortedDictionary<int, TextData> _texts = new SortedDictionary<int, TextData>();
        private readonly SortedDictionary<int, MoveImageData> _moves = new SortedDictionary<int, MoveImageData>();

        public FrameBuilder(int index) { this.index = index; }

        public void AddText(int slot, TextData t, List<string> errors, string ctx)
        {
            if (_texts.ContainsKey(slot)) { errors.Add($"{ctx}: text slot 중복"); return; }
            _texts[slot] = t;
        }

        public void AddMove(int slot, MoveImageData m, List<string> errors, string ctx)
        {
            if (_moves.ContainsKey(slot)) { errors.Add($"{ctx}: move slot 중복"); return; }
            _moves[slot] = m;
        }

        public void Validate(string id, List<string> errors, int maxMove, int maxText)
        {
            CheckSlots(_texts.Keys, $"[{id}] f{index} text", maxText, errors);
            CheckSlots(_moves.Keys, $"[{id}] f{index} move", maxMove, errors);
        }

        private static void CheckSlots(IEnumerable<int> slots, string label, int max, List<string> errors)
        {
            int expected = 0;
            foreach (int s in slots) // SortedDictionary 라 오름차순
            {
                if (s != expected)
                    errors.Add($"{label} 슬롯이 0부터 연속이 아님(슬롯 {expected} 누락). 플레이어가 인덱스로 풀에 매핑하므로 어긋남.");
                if (s >= max)
                    errors.Add($"{label} 슬롯 {s} 가 풀 크기({max})를 넘음 → 런타임에서 무시됨.");
                expected++;
            }
        }

        public CutsceneFrame Build() => new CutsceneFrame
        {
            bgmChange = bgmChange ?? new BGMChangeData(),
            itemImage = item ?? new ItemImageData(),
            Texts = _texts.Values.ToList(),
            moveImages = _moves.Values.ToList()
        };
    }

    // ============================================================
    //  파싱 헬퍼
    // ============================================================
    private static FaidInOut Fade(Table t, List<string> row, string useK, string delayK, string inK, string outK, string ctx, List<string> errors)
        => new FaidInOut
        {
            useFade = ParseBool(t.Get(row, useK)),
            startDelay = ParseFloat(t.Get(row, delayK), $"{ctx} {delayK}", errors),
            fadeInDuration = ParseFloat(t.Get(row, inK), $"{ctx} {inK}", errors),
            fadeOutDuration = ParseFloat(t.Get(row, outK), $"{ctx} {outK}", errors)
        };

    private static Vector2 V2(string xs, string ys, string ctx, List<string> errors)
        => new Vector2(ParseFloat(xs, $"{ctx} x", errors), ParseFloat(ys, $"{ctx} y", errors));

    private static float ParseFloat(string s, string ctx, List<string> errors)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return 0f;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        errors.Add($"{ctx}: 숫자 파싱 실패 '{s}'");
        return 0f;
    }

    private static int ParseInt(string s, string ctx, List<string> errors)
    {
        s = (s ?? "").Trim();
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        errors.Add($"{ctx}: 정수 파싱 실패 '{s}'");
        return 0;
    }

    private static bool ParseBool(string s)
    {
        s = (s ?? "").Trim();
        return s.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || s == "1";
    }

    private static T ParseEnum<T>(string s, T fallback, string ctx, List<string> errors) where T : struct
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return fallback;
        // 이름("OutCubic") 과 숫자("0", 이름 없는 enum 값) 둘 다 허용
        if (Enum.TryParse<T>(s, out var v)) return v;
        errors.Add($"{ctx}: enum({typeof(T).Name}) 파싱 실패 '{s}'");
        return fallback;
    }

    // \n 리터럴(역슬래시+n) → 실제 줄바꿈. 트림하지 않음.
    private static string Unescape(string s) => (s ?? "").Replace("\\n", "\n");

    private static Sprite ResolveSprite(string token, string ctx, List<string> errors)
    {
        token = (token ?? "").Trim();
        if (token.Length == 0) return null;

        int b = token.IndexOf('[');
        if (b >= 0 && token.EndsWith("]"))
        {
            string path = token.Substring(0, b);
            string sub = token.Substring(b + 1, token.Length - b - 2);
            var sp = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(x => x.name == sub);
            if (sp == null) errors.Add($"{ctx}: 서브 스프라이트 '{sub}' 를 '{path}' 에서 찾지 못함");
            return sp;
        }
        else
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(token);
            if (sp == null) errors.Add($"{ctx}: 스프라이트 로드 실패 '{token}'");
            return sp;
        }
    }

    // ============================================================
    //  CSV 로딩 / 파싱
    // ============================================================
    private static Table LoadTable(string fileName, List<string> errors)
    {
        string rel = Path.Combine(InputDir, fileName);
        string abs = Path.GetFullPath(rel);
        if (!File.Exists(abs)) { errors.Add($"CSV 없음: {rel}"); return null; }

        var rows = ParseCsv(File.ReadAllText(abs, Encoding.UTF8));
        if (rows.Count == 0) { errors.Add($"빈 CSV: {fileName}"); return null; }
        return new Table(rows, fileName, errors);
    }

    // RFC4180 풍 파서: 따옴표 내장 쉼표/줄바꿈, "" 이스케이프, 선행 BOM 처리.
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        int i = 0;
        if (text.Length > 0 && text[0] == '\uFEFF') i = 1; // BOM

        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* 무시 */ }
                else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }

        // 완전 빈 행 제거
        return rows.Where(r => r.Any(c => c.Trim().Length > 0)).ToList();
    }

    private class Table
    {
        private readonly Dictionary<string, int> _map = new Dictionary<string, int>();
        public List<List<string>> Rows { get; }

        public Table(List<List<string>> all, string fileName, List<string> errors)
        {
            var header = all[0];
            for (int i = 0; i < header.Count; i++)
            {
                string key = header[i].Trim();
                if (!string.IsNullOrEmpty(key) && !_map.ContainsKey(key)) _map[key] = i;
            }
            Rows = all.Skip(1).ToList();
        }

        public string Get(List<string> row, string col)
            => _map.TryGetValue(col, out int i) && i < row.Count ? row[i] : "";
    }

    private static void Abort(List<string> errors)
    {
        Debug.LogError($"[CutsceneImporter] 에러 {errors.Count}건. 아무것도 굽지 않고 중단합니다:\n - " + string.Join("\n - ", errors));
    }

    // CustomLog 의존성 없이 안전하게 로그 (프로젝트에 CustomLog 가 있으면 그걸 써도 됨)
    private static void CustomLogSafe(string msg) => Debug.Log(msg);
}
#endif