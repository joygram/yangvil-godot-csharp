using Godot;

// YangVil Unity 판 Intro.cs 이식. screen_id = intro
namespace YangVillage.Ui
{
    // 첫 화면: 제목 · 마스코트 셋. 준비가 끝나면 잠깐 보여 주고 마을로 넘어간다.
    public partial class Intro : Control
    {
        const float MascotSize = 190;

        public override async void _Ready()
        {
            Kit.Root(this, Kit.Sky);
            var grass = Kit.Rect(this, Kit.Grass);
            grass.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            grass.OffsetTop = -420;

            var column = Kit.Column(this, 32, 18);
            Kit.CenterColumn(column);
            Kit.Label(column, App.T("ui_title"), 84);
            Kit.Label(column, App.T("ui_tagline"), 24);

            var mascots = Kit.Row(column, 4);
            var db = App.Db;
            var starter = App.Game.CatSprite(db.Config("starter_cat_id"));
            for (var stage = 2; stage >= 0; stage--)
            {
                var size = MascotSize * db.ConfigInt("growth_scale_pct_" + stage) / 100f;
                SpriteView.Of(starter, size, mascots).SizeFlagsVertical = SizeFlags.ShrinkEnd;
            }

            Kit.Label(column, App.T("ui_version", db.Config("version")), 16, Kit.Muted);
            if (App.Capturing) return;
            await ToSignal(GetTree().CreateTimer(db.ConfigInt("intro_hold_ms") / 1000f), SceneTreeTimer.SignalName.Timeout);
            if (IsInstanceValid(this) && IsInsideTree()) App.Go("village");
        }
    }
}
