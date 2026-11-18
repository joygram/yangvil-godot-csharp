using Godot;
using YangVillage.Core;

// YangVil Unity 판 Boot.cs 이식. 시작 씬(boot.tscn)의 루트.
// 테이블 점검 후 config.start_screen 으로 넘어감.
namespace YangVillage.Ui
{
    public partial class Boot : Control
    {
        public override void _Ready()
        {
            App.Root = this;
            var errors = TableCheck.Run(App.Tables);
            foreach (var error in errors) GD.PushError(error);
            if (errors.Count > 0) return;
            App.Go(App.Db.Config("start_screen"));
        }
    }
}
