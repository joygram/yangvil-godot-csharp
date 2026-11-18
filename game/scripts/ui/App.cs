using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YangVillage.Core;

// YangVil Unity 판 App.cs 이식.
// 테이블 = res://data/tables/*.tsv. 화면 전환 = 부트 루트 안에서 화면 노드를 갈아 끼움 (Unity 판과 같은 구조). screen 테이블의 screen_id 그대로.
namespace YangVillage.Ui
{
    public static class App
    {
        const string TableFolder = "res://data/tables";
        const string SavePath = "user://yangvil_save.json";
        static Game _game;
        static TableSet _tables;

        public static TableSet Tables => _tables ??= LoadTables();
        public static Game Game => _game ??= new Game(new Db(Tables), Load, Save);
        public static Db Db => Game.Db;

        public static string StageId;
        public static bool Won;
        public static BattleReward Reward;

        // 화면 갈무리 중: 화면이 스스로 넘어가지 않는다.
        public static bool Capturing;

        public static string T(string textId, params object[] args) => args.Length == 0 ? Db.Text(textId) : Db.Text(textId, args);

        // screen_id → 화면.
        static readonly Dictionary<string, Func<Control>> Screens = new Dictionary<string, Func<Control>>
        {
            { "intro", () => new Intro() },
            { "village", () => new Village() },
            { "stage_select", () => new StageSelect() },
            { "battle", () => new BattleScreen() },
            { "result", () => new Result() },
        };

        public static Control Root;
        static Control _current;

        public static void Go(string screenId)
        {
            Db.Row("screen", "screen_id", screenId);
            _current?.QueueFree();
            Engine.TimeScale = 1;
            var screen = Screens[screenId]();
            screen.Name = screenId;
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Root.AddChild(screen);
            _current = screen;
            // 측정·검사 신호 (scripts/web/play.mjs)
            GD.Print($"[screen] {screenId}");
        }

        public static IEnumerable<string> ScreenIds => Screens.Keys;

        public static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

        static TableSet LoadTables()
        {
            var names = DirAccess.GetFilesAt(TableFolder).Where(f => f.EndsWith(".tsv")).Select(f => f.Substring(0, f.Length - 4));
            return TableSet.Load(names.OrderBy(n => n, StringComparer.Ordinal), name => FileAccess.GetFileAsString($"{TableFolder}/{name}.tsv"));
        }

        static string Load() => FileAccess.FileExists(SavePath) ? FileAccess.GetFileAsString(SavePath) : null;

        static void Save(string text)
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(text);
        }
    }
}
