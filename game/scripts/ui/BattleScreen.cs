using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using YangVillage.Battle;
using YangVillage.Core;

// YangVil Unity 판 BattleScreen.cs 이식. screen_id = battle
// 그리기 층: 무대(배경·어둡게·전장) → HUD 바탕 → 조작 줄 → 전장 위 연출(z_index) → 번쩍·컷인.
namespace YangVillage.Ui
{
    // 전투: 왼쪽 우리 파티 · 오른쪽 적 무리가 마주 선 전장. 돌진 → 타격 → 넉백 → 복귀.
    public partial class BattleScreen : Control
    {
        const float Cell = 68;
        const float UnitHeight = 1.8f * Cell;
        const float FieldHeight = 700;
        static readonly Vector2 FieldOrigin = new Vector2(270, 380);
        static readonly Vector2[] AllySlots = { new Vector2(-2.2f, 1.2f), new Vector2(-2.65f, -1.2f), new Vector2(-2.1f, -3.6f) };
        static readonly Vector2[] EnemySlots = { new Vector2(2.2f, 1.2f), new Vector2(2.65f, -1.2f), new Vector2(2.1f, -3.6f), new Vector2(1f, -2.5f) };

        Game G => App.Game;
        Db Db => App.Db;
        BattleState _state;
        string _stageId;
        Node2D _stage, _field, _overlay;
        Vector2 _stageHome;
        Vector2 _size;
        Label _title, _status;
        ColorRect _dim, _flash;
        HBoxContainer _skillRow, _handRow;
        TaskCompletionSource<List<BattleCard>> _command;
        readonly List<BattleCard> _picks = new List<BattleCard>();
        readonly Dictionary<BattleCard, Button> _cardButtons = new Dictionary<BattleCard, Button>();
        bool _auto;
        Button _speedButton, _autoButton, _pauseButton;
        readonly Dictionary<BattleUnit, BattleActor> _actors = new Dictionary<BattleUnit, BattleActor>();
        readonly Dictionary<BattleUnit, (PanelContainer panel, Label label)> _panels = new Dictionary<BattleUnit, (PanelContainer, Label)>();
        readonly Random _rng = new Random();
        int _speed = 1;
        bool _paused;
        BattleBackdrop _backdrop;

        static Vector2 Slot(Vector2 slot) => new Vector2(slot.X * Cell, -slot.Y * Cell);
        bool Here => IsInstanceValid(this) && IsInsideTree();

        public override void _Ready()
        {
            Kit.Root(this, Kit.Grass);
            _stageId = App.StageId;
            _auto = OS.GetCmdlineArgs().Contains("--auto") || OS.GetCmdlineUserArgs().Contains("--auto");
            _state = G.BuildBattle(_stageId);
            _size = GetViewportRect().Size;

            _stage = Kit.Add(this, new Node2D { Name = "stage" });
            _backdrop = Kit.Add(_stage, new BattleBackdrop { Area = _size, FieldHeight = FieldHeight, Picture = Sprites.Frames(Db.Config("battle_backdrop_sprite_id"))[0] });
            _dim = Kit.Add(_stage, new ColorRect { Name = "dim", Position = -_size / 4, Size = _size * 1.5f, Color = FxMath.A(Kit.Night, 0), MouseFilter = MouseFilterEnum.Ignore });
            // 전장(배우·연출)은 z_index 대로 HUD 위에 그린다.
            _field = Kit.Add(_stage, new Node2D { Name = "field", Position = FieldOrigin });
            Kit.Add(this, new BattleBackdrop { Name = "hud_shade", Area = _size, FieldHeight = FieldHeight });

            var touch = Kit.Rect(this, Colors.Transparent, true);
            touch.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } m) OnFieldInput(m.Position);
            };

            var column = Kit.Column(this, 12, 6);
            _title = Kit.Label(column, "", 20, Kit.Paper);
            var statusPanel = Kit.Card(column);
            ((Control)statusPanel.GetParent()).MouseFilter = MouseFilterEnum.Ignore;
            _status = Kit.Label(statusPanel, App.T("ui_battle_start"), 17);
            Kit.Spacer(column);

            _skillRow = Kit.Row(column, 6);
            _skillRow.CustomMinimumSize = new Vector2(0, 52);
            _handRow = Kit.Row(column, 6);
            _handRow.CustomMinimumSize = new Vector2(0, 124);

            var panels = Kit.Row(column, 6);
            foreach (var ally in _state.Allies)
            {
                var panel = Kit.Panel(panels, Kit.Paper, Kit.Ink, 16, 10);
                Kit.Expand(panel);
                var label = Kit.Label(panel, "", 13);
                _panels[ally] = (panel, label);
            }

            var controls = Kit.Row(column, 6);
            _speedButton = Kit.Button(controls, "", () =>
            {
                _speed = _speed > 1 ? 1 : Db.ConfigInt("battle_fast_speed");
                ApplyTime();
            }, 16);
            _autoButton = Kit.Button(controls, "", () =>
            {
                _auto = !_auto;
                if (_auto && _command != null) Finish(_state.AutoPick());
                RefreshAll();
            }, 16);
            _pauseButton = Kit.Button(controls, "", () =>
            {
                _paused = !_paused;
                ApplyTime();
            }, 16);
            Kit.Button(controls, App.T("ui_retreat"), () => App.Go("stage_select"), 16);

            _overlay = Kit.Add(this, new Node2D { Name = "overlay" });
            _flash = Kit.Add(_overlay, new ColorRect { Name = "flash", Size = _size, Color = FxMath.A(Colors.White, 0), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3000 });

            for (var i = 0; i < _state.Allies.Count; i++) Spawn(_state.Allies[i], AllySlots[i % AllySlots.Length]);
            SpawnWave();
            RefreshAll();
            Run();
        }

        public override void _ExitTree() => Engine.TimeScale = 1;

        void ApplyTime()
        {
            Engine.TimeScale = _paused ? 0 : _speed;
            RefreshAll();
        }

        // 배치
        BattleActor Spawn(BattleUnit unit, Vector2 slot)
        {
            // 멀리(위) 선 유닛은 작게, 가까이(아래) 선 유닛은 크게.
            var near = Mathf.InverseLerp(AllySlots.Max(s => s.Y), AllySlots.Min(s => s.Y), slot.Y);
            var depth = Mathf.Lerp(Db.ConfigInt("depth_far_pct"), Db.ConfigInt("depth_near_pct"), near) / 100f;
            var height = UnitHeight * depth * Db.ConfigInt(unit.IsBoss ? "boss_scale_pct" : unit.IsAlly ? "ally_scale_pct" : "enemy_scale_pct") / 100f;
            var actor = BattleActor.Of(_field, unit, Slot(slot), height);
            _actors[unit] = actor;
            return actor;
        }

        void SpawnWave()
        {
            for (var i = 0; i < _state.Enemies.Count; i++) Spawn(_state.Enemies[i], EnemySlots[i % EnemySlots.Length]);
            _title.Text = App.T("ui_battle_wave", G.StageName(_stageId), _state.Wave + 1, _state.WaveCount);
        }

        void OnFieldInput(Vector2 click)
        {
            var at = click - FieldOrigin;
            var hit = _actors.Values.FirstOrDefault(a => !a.Unit.IsAlly && a.Unit.Alive && a.HitBox.HasPoint(at));
            if (hit == null) return;
            _state.FocusTarget = _state.FocusTarget == hit.Unit ? null : hit.Unit;
            RefreshAll();
        }

        void RefreshAll()
        {
            foreach (var actor in _actors.Values) actor.Refresh(_state.FocusTarget == actor.Unit);
            foreach (var pair in _panels)
            {
                var unit = pair.Key;
                var (panel, label) = pair.Value;
                var ready = unit.Alive && unit.GaugeFull;
                var tag = !unit.Alive ? App.T("ui_tag_down") : ready ? App.T("ui_tag_ready") : App.T("ui_tag_gauge", unit.Gauge, BattleUnit.GaugeMax);
                label.Text = App.T("ui_status", unit.Name, unit.Hp, unit.MaxHp, tag);
                panel.AddThemeStyleboxOverride("panel", ready ? Kit.Box(Kit.Gold, Kit.Ink, 12, 10) : Kit.Box(Kit.Paper, Kit.Ink, 16, 10));
                panel.Modulate = unit.Alive ? Colors.White : Kit.Down;
            }
            _speedButton.Text = App.T("ui_speed", _speed);
            _autoButton.Text = App.T(_auto ? "ui_auto_on" : "ui_auto_off");
            _pauseButton.Text = App.T(_paused ? "ui_resume" : "ui_pause");
        }

        // 카드 고르기: 손패에서 정해진 장수를 고르면 바로 행동이 시작된다.
        Task<List<BattleCard>> Command()
        {
            _state.Deal();
            _command = new TaskCompletionSource<List<BattleCard>>();
            _picks.Clear();
            foreach (var card in _state.SkillCards()) CardButton(_skillRow, card, 15, new Vector2(150, 52));
            foreach (var card in _state.Hand) CardButton(_handRow, card, 14, new Vector2(0, 124));
            UpdateCommand();
            return _command.Task;
        }

        void CardButton(Node row, BattleCard card, int fontSize, Vector2 minSize)
        {
            var color = card.IsSkill ? Kit.Night : Kit.Hex(card.Def.Color);
            var button = Kit.Button(row, "", () => Toggle(card), fontSize);
            Kit.Restyle(button, color, card.IsSkill ? Kit.Gold : Kit.Ink, 12);
            button.CustomMinimumSize = minSize;
            if (card.IsSkill) button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            else Kit.ButtonIcon(button, Sprites.Frames(card.Owner.SpriteId)[0], 64);
            Kit.SetTextColor(button, card.IsSkill ? Kit.Gold : Kit.Paper);
            _cardButtons[card] = button;
        }

        void Toggle(BattleCard card)
        {
            if (_command == null) return;
            if (!_picks.Remove(card)) _picks.Add(card);
            UpdateCommand();
            if (_picks.Count >= Math.Min(_state.PickCount, _cardButtons.Count)) Finish(_picks.ToList());
        }

        void UpdateCommand()
        {
            foreach (var pair in _cardButtons)
            {
                var card = pair.Key;
                var button = pair.Value;
                var order = _picks.IndexOf(card);
                var name = card.IsSkill ? App.T("ui_card_skill", card.Owner.Name) : card.Def.Name;
                button.Text = order < 0 ? name : App.T("ui_card_picked", order + 1, name);
                button.Modulate = order < 0 ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
            }
            _status.Text = App.T("ui_command", _state.PickCount, _picks.Count);
        }

        void Finish(List<BattleCard> picks)
        {
            foreach (var button in _cardButtons.Values) button.QueueFree();
            _cardButtons.Clear();
            var command = _command;
            _command = null;
            command?.TrySetResult(picks);
        }

        void Dim(bool on) =>
            _dim.CreateTween().TweenProperty(_dim, "color:a", on ? Db.ConfigInt("skill_dim_pct") / 100f : 0f, 0.15f);

        // 진행
        async Task<bool> Wait(float seconds, bool realTime = false)
        {
            await ToSignal(GetTree().CreateTimer(seconds, true, false, realTime), SceneTreeTimer.SignalName.Timeout);
            return Here;
        }

        async Task<bool> Move(Node2D node, Vector2 to, float seconds, Tween.EaseType ease)
        {
            var tween = node.CreateTween();
            tween.TweenProperty(node, "position", to, seconds).SetTrans(Tween.TransitionType.Quad).SetEase(ease);
            await ToSignal(tween, Tween.SignalName.Finished);
            return Here;
        }

        async void Run()
        {
            Engine.TimeScale = _speed;
            if (!await Advance()) return;
            while (_state.Outcome == BattleOutcome.Ongoing)
            {
                if (_state.WaveCleared)
                {
                    if (!await NextWave()) return;
                    continue;
                }
                if (_state.NeedsCommand)
                {
                    var picks = _auto ? _state.AutoPick() : await Command();
                    if (!Here) return;
                    _state.Commit(picks);
                    if (_state.Chain != null)
                    {
                        _status.Text = App.T("ui_log_chain", _state.Chain.Name);
                        if (!await Wait(0.5f)) return;
                    }
                }
                var action = _state.Step();
                if (action == null) continue;
                if (!await Animate(action)) return;
                RefreshAll();
                if (!await Wait(0.2f)) return;
            }
            if (!await Wait(0.8f)) return;
            App.Won = _state.Outcome == BattleOutcome.Victory;
            App.Reward = App.Won ? G.ApplyVictory(_stageId, _state.Stars()) : null;
            App.Go("result");
        }

        async Task<bool> NextWave()
        {
            if (!await Wait(0.6f)) return false;
            _state.AdvanceWave();
            _status.Text = App.T("ui_log_wave", _state.Wave + 1, _state.WaveCount);
            SpawnWave();
            if (!await Advance()) return false;
            foreach (var passed in _actors.Keys.Where(u => !u.IsAlly && !_state.Enemies.Contains(u)).ToList())
            {
                _actors[passed].QueueFree();
                _actors.Remove(passed);
            }
            RefreshAll();
            return await Wait(0.3f);
        }

        static float Angle(Vector2 v) => Mathf.Atan2(v.Y, v.X);

        async Task<bool> Animate(BattleAction action)
        {
            var actor = _actors[action.Actor];
            var skill = action.Kind == ActionKind.Skill;
            _status.Text = skill ? App.T("ui_log_skill", action.Actor.Name) : action.Extra ? App.T("ui_log_extra", action.Actor.Name)
                : action.Card != null ? App.T("ui_log_card", action.Actor.Name, action.Card.Def.Name) : App.T("ui_log_card", action.Actor.Name, App.T("ui_attack"));
            if (action.Hits.Count == 0)
            {
                actor.Glow();
                return await Wait(0.3f);
            }
            if (skill)
            {
                Dim(true);
                var seconds = Db.ConfigInt("cut_in_ms") / 1000f;
                SkillCutIn.Play(_overlay, _size, App.T("ui_cut_in", action.Actor.Name), Sprites.ElementColor(action.Actor.ElementId), seconds);
                if (!await Wait(seconds)) return false;
                // 기운 모으기: 발밑으로 고리가 조여들며 몸이 빛난다.
                Gather(action.Actor, actor);
                actor.Glow();
                if (!await Wait(0.32f)) return false;
            }

            var target = _actors[action.Hits[0].target];
            var side = action.Actor.IsAlly ? 1 : -1;
            var single = action.Hits.All(h => h.target == action.Hits[0].target);
            // 한 대상이면 바로 앞까지 붙고, 여럿이면 한 걸음만 나선다.
            var reach = single
                ? target.Home + new Vector2(-side * target.Head * Db.ConfigInt("reach_gap_pct") / 100f, 1)
                : actor.Home + new Vector2(side * Cell, 0);

            var moves = Style(action.Actor, "moves");
            actor.Crouch();
            if (!await Wait(0.14f)) return false;
            if (moves)
            {
                Puff(actor.Home);
                for (var i = 1; i <= 3; i++) Trail(actor, actor.Home.Lerp(reach, i / 4f), i * 0.025f);
                actor.ZIndex += 400;
                if (!await Move(actor, reach, 0.12f, Tween.EaseType.In)) return false;
            }
            Zoom(single ? ((moves ? reach : target.Home) + target.Home) / 2 : Vector2.Zero, Db.ConfigInt(skill ? "skill_zoom_pct" : "zoom_pct") / 100f, 0.08f);

            for (var i = 0; i < action.Hits.Count; i++)
            {
                var (unit, damage, affinityPct) = action.Hits[i];
                var struck = _actors[unit];
                var last = i == action.Hits.Count - 1;
                if (i > 0 && !await Wait(Db.ConfigInt("hit_gap_ms") / 1000f)) return false;
                var aim = (Chest(struck) - Chest(actor)).Normalized();
                if (skill && i == 0 && !await Flurry(actor, struck, aim, moves)) return false;
                if (moves)
                {
                    // 당겼다 내지르는 끝에 타격을 맞춘다.
                    actor.Strike(aim, actor.Head * Db.ConfigInt("strike_reach_pct") / 100f);
                    if (!await Wait(BattleActor.StrikeLead)) return false;
                }
                else if (!await Launch(actor, struck, aim)) return false;
                if (Style(action.Actor, "slash") || skill) Slash(actor, struck, aim, moves, i % 2 == 0 ? 1 : -1, skill ? 1.25f : 1);
                Impact(action.Actor, struck, damage, affinityPct, skill || action.Extra, aim);
                var heavy = skill && last;
                Shake(Db.ConfigInt(heavy ? "skill_shake_px" : "shake_px"), aim);
                if (!await HitStop(heavy ? "skill_hit_stop_ms" : "hit_stop_ms")) return false;
            }

            if (!await Wait(Db.ConfigInt("shake_ms") / 1000f + 0.06f)) return false;
            Zoom(Vector2.Zero, 1, 0.16f);
            if (moves)
            {
                actor.Hop(0.24f, actor.Head * 0.45f);
                if (!await Move(actor, actor.Home, 0.24f, Tween.EaseType.Out)) return false;
                Puff(actor.Home);
                actor.ZIndex -= 400;
            }
            if (skill) Dim(false);
            return true;
        }

        bool Style(BattleUnit unit, string column) => Db.Get("attack_style", "attack_style_id", unit.StyleId, column) == "true";

        bool Burns(BattleUnit unit) => Db.Get("element", "element_id", unit.ElementId, "burns") == "true";

        static Color Tint(BattleUnit unit) => Sprites.ElementColor(unit.ElementId).Lerp(Colors.White, 0.3f);

        Sprite2D FieldSprite(Texture2D texture, Vector2 at, Vector2 scale, int z)
        {
            var s = new Sprite2D { Texture = texture, Position = at, Scale = scale, ZIndex = z };
            _field.AddChild(s);
            return s;
        }

        // 원거리: 쏘는 직업은 몸에서 곧장, 주문 직업은 발밑에 고리를 띄우고 대상 머리 위에서 떨어뜨린다.
        async Task<bool> Launch(BattleActor actor, BattleActor target, Vector2 aim)
        {
            var cast = Style(actor.Unit, "cast");
            var hit = Chest(target);
            var from = cast ? hit + new Vector2(-Mathf.Sign(aim.X) * target.Head * 1.3f, -target.Head * 2.6f) : Chest(actor) + aim * actor.Head * 0.4f;
            if (cast)
            {
                Gather(actor.Unit, actor);
                if (!await Wait(0.2f)) return false;
            }
            actor.Recoil(aim, actor.Head * 0.22f);
            var texture = Sprites.Frames(Db.Get("element", "element_id", actor.Unit.ElementId, "shot_sprite_id"))[0];
            var size = target.Head * (cast ? 0.9f : 0.6f) / texture.GetHeight();
            // 그림은 머리가 오른쪽이다. 왼쪽으로 날 때는 돌린 뒤 위아래를 뒤집어 바로 세운다.
            var fly = hit - from;
            Sprite2D Shot(Vector2 at, float scale, int z)
            {
                var s = FieldSprite(texture, at, Vector2.One * size * scale, z);
                s.Rotation = Angle(fly);
                s.FlipV = fly.X < 0;
                return s;
            }
            var shot = Shot(from, 1, 1500);
            const float flight = 0.14f;
            for (var i = 1; i <= 4; i++)
            {
                var tail = Shot(from.Lerp(hit, i / 5f), 0.8f, 1490);
                tail.Modulate = FxMath.A(Colors.White, 0);
                var tw = tail.CreateTween();
                tw.TweenInterval(flight * i / 5);
                tw.TweenProperty(tail, "modulate:a", 0.45f, 0.01f);
                tw.TweenProperty(tail, "modulate:a", 0f, 0.12f);
                tw.TweenCallback(Callable.From(tail.QueueFree));
            }
            var here = await Move(shot, hit, flight, Tween.EaseType.In);
            shot.QueueFree();
            return here;
        }

        static Vector2 Chest(BattleActor actor) => actor.Position + new Vector2(0, -actor.Head * 0.5f);

        // 베기 궤적: 붙어서 칠 때는 때리는 쪽 몸을 축으로, 멀리서 칠 때는 대상 바로 앞을 축으로 대상을 가로지른다.
        void Slash(BattleActor actor, BattleActor target, Vector2 aim, bool moves, int sweep, float scale)
        {
            var pivot = moves ? Chest(actor) : Chest(target) - aim * target.Head;
            SlashFx.Play(_field, pivot, ((Chest(target) - pivot).Length() + target.Head * 0.25f) * scale, Angle(aim), sweep, Tint(actor.Unit));
        }

        async Task<bool> HitStop(string key)
        {
            Engine.TimeScale = 0;
            if (!await Wait(Db.ConfigInt(key) / 1000f, true)) return false;
            Engine.TimeScale = _paused ? 0 : _speed;
            return true;
        }

        // 필살 난타: 본타 앞에 빠른 타격을 여러 번 꽂는다. 타마다 내지르고, 밀리고, 짧게 멈춘다.
        async Task<bool> Flurry(BattleActor actor, BattleActor target, Vector2 aim, bool moves)
        {
            for (var i = 0; i < Db.ConfigInt("skill_flurry_hits"); i++)
            {
                if (moves)
                {
                    actor.Strike(aim, actor.Head * Db.ConfigInt("strike_reach_pct") / 200f);
                    if (!await Wait(BattleActor.StrikeLead)) return false;
                }
                var jitter = new Vector2((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1)) * target.Head * 0.2f;
                Slash(actor, target, aim, moves, i % 2 == 0 ? 1 : -1, 0.9f + i % 3 * 0.1f);
                SparkFx.Play(_field, Chest(target) + jitter, target.Head * 0.7f, Tint(actor.Unit), 0.16f);
                target.Hurt(aim, Db.ConfigInt("knockback_px") * 0.5f, Burns(actor.Unit));
                Shake(Db.ConfigInt("shake_px") / 2, aim);
                if (!await HitStop("flurry_hit_stop_ms")) return false;
                if (!moves && !await Wait(0.06f)) return false;
            }
            return true;
        }

        void Gather(BattleUnit unit, BattleActor actor)
        {
            var texture = Sprites.Frames(Db.Get("element", "element_id", unit.ElementId, "skill_sprite_id"))[0];
            var size = actor.Head * 2.2f / texture.GetHeight();
            var ring = FieldSprite(texture, actor.Home, new Vector2(size, size * 0.4f), actor.ZIndex - 1);
            ring.Modulate = FxMath.A(Colors.White, 0);
            var tw = ring.CreateTween();
            tw.TweenProperty(ring, "modulate:a", 1f, 0.08f);
            tw.Parallel().TweenProperty(ring, "scale", new Vector2(size, size * 0.4f) * 0.45f, 0.3f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenProperty(ring, "modulate:a", 0f, 0.08f);
            tw.TweenCallback(Callable.From(ring.QueueFree));
        }

        // 무대 확대: 전장 좌표의 한 점을 중심으로 당겨 잡는다.
        void Zoom(Vector2 fieldPoint, float scale, float seconds)
        {
            var pivot = FieldOrigin + fieldPoint;
            _stageHome = pivot * (1 - scale);
            var tw = _stage.CreateTween().SetParallel();
            tw.TweenProperty(_stage, "scale", Vector2.One * scale, seconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(_stage, "position", _stageHome, seconds).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        void Puff(Vector2 at)
        {
            var texture = Sprites.Frames(Db.Config("dust_sprite_id"))[0];
            var grow = Cell * 0.9f / texture.GetWidth();
            var dust = FieldSprite(texture, at, Vector2.One * grow * 0.4f, 200);
            dust.Modulate = FxMath.A(Kit.Cream, 0.7f);
            var tw = dust.CreateTween();
            tw.TweenProperty(dust, "scale", Vector2.One * grow, 0.25f).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(dust, "modulate:a", 0f, 0.25f);
            tw.TweenCallback(Callable.From(dust.QueueFree));
        }

        void Trail(BattleActor actor, Vector2 at, float delay)
        {
            var ghost = actor.Ghost(_field, at);
            ghost.Visible = false;
            var tw = ghost.CreateTween();
            tw.TweenInterval(delay);
            tw.TweenCallback(Callable.From(() => ghost.Visible = true));
            tw.TweenProperty(ghost, "modulate:a", 0f, 0.18f);
            tw.TweenCallback(Callable.From(ghost.QueueFree));
        }

        // 등장: 화면 밖에서 총총 걸어 들어온다.
        // 전진: 아군은 제자리에서 걷고 땅이 뒤로 흐른다. 쓰러진 적은 땅과 함께 뒤로 밀려나고 새 적은 앞에서 다가온다.
        async Task<bool> Advance()
        {
            var distance = Db.ConfigInt("advance_px");
            var seconds = Db.ConfigInt("advance_ms") / 1000f;
            var flow = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            flow.TweenMethod(Callable.From<float>(x => _backdrop.Scroll = x), _backdrop.Scroll, _backdrop.Scroll + distance, seconds);
            foreach (var actor in _actors.Values)
            {
                if (actor.Unit.IsAlly)
                {
                    if (actor.Unit.Alive) actor.Walk(seconds);
                    continue;
                }
                if (actor.Unit.Alive) actor.Position = actor.Home + new Vector2(distance, 0);
                flow.TweenProperty(actor, "position:x", actor.Position.X - distance, seconds);
            }
            await ToSignal(flow, Tween.SignalName.Finished);
            return Here;
        }

        void Impact(BattleUnit attacker, BattleActor target, int damage, int affinityPct, bool skill, Vector2 blow)
        {
            var at = target.Home + new Vector2(0, -target.Head * 0.5f);
            Burst(attacker, "hit_sprite_id", at, target.Head * 1.1f, 0);
            SparkFx.Play(_field, at, target.Head * (skill ? 1.5f : 0.9f), Tint(attacker));
            if (skill)
            {
                // 필살: 큰 고리 + 주변에 잇따라 터지는 불꽃 + 화면 번쩍.
                Burst(attacker, "skill_sprite_id", at, target.Head * 2.4f, 0);
                for (var i = 0; i < 4; i++)
                {
                    var spread = new Vector2((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1)) * target.Head * 0.6f;
                    Burst(attacker, "hit_sprite_id", at + spread, target.Head * 0.8f, 0.05f + i * 0.05f);
                }
                _flash.Color = FxMath.A(Sprites.ElementColor(attacker.ElementId).Lerp(Colors.White, 0.6f), 0.7f);
                _flash.CreateTween().TweenProperty(_flash, "color:a", 0f, 0.3f);
            }

            target.Hurt(blow, Db.ConfigInt("knockback_px") * (skill ? 1.6f : 1), Burns(attacker));

            var tag = affinityPct > 100 ? "\n" + App.T("ui_pop_strong") : affinityPct < 100 ? "\n" + App.T("ui_pop_weak") : "";
            var color = affinityPct > 100 ? Kit.Gold : affinityPct < 100 ? Kit.Muted : Kit.Paper;
            // 숫자 상자 (220x80) 가운데를 축으로 커졌다 줄며 떠오른다.
            var pop = Kit.Add(_field, new Node2D { Name = "pop", ZIndex = 2000, Position = target.Home + new Vector2(0, -target.Head - 70), Scale = Vector2.One * 1.9f });
            var label = Kit.Label(pop, App.T("ui_pop_damage", damage) + tag, skill ? 40 : 30, color);
            Kit.PlaceCentered(label, new Vector2(-110, -40), new Vector2(220, 80));
            Kit.Outline(label, Kit.Ink, 8);
            var tw = pop.CreateTween();
            tw.TweenProperty(pop, "scale", Vector2.One, 0.14f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(pop, "position:y", pop.Position.Y - 0.9f * Cell, 0.8f);
            tw.Parallel().TweenProperty(pop, "modulate:a", 0f, 0.8f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tw.TweenCallback(Callable.From(pop.QueueFree));
            target.Refresh(_state.FocusTarget == target.Unit);
        }

        void Burst(BattleUnit attacker, string column, Vector2 at, float size, float delay)
        {
            var texture = Sprites.Frames(Db.Get("element", "element_id", attacker.ElementId, column))[0];
            var grow = size / texture.GetHeight();
            var fx = FieldSprite(texture, at, Vector2.One * grow * 0.3f, 1500);
            fx.Modulate = FxMath.A(Colors.White, 0);
            var tw = fx.CreateTween();
            tw.TweenInterval(delay);
            tw.TweenProperty(fx, "modulate:a", 1f, 0.01f);
            tw.TweenProperty(fx, "scale", Vector2.One * grow, 0.14f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(fx, "rotation", 0.5f, 0.3f);
            tw.TweenProperty(fx, "modulate:a", 0f, 0.2f);
            tw.TweenCallback(Callable.From(fx.QueueFree));
        }

        // 흔들림은 때린 방향을 따라 오간다.
        void Shake(int strength, Vector2 axis)
        {
            const int steps = 6;
            var step = Db.ConfigInt("shake_ms") / 1000f / steps;
            var tween = _stage.CreateTween();
            for (var i = 0; i < steps; i++)
            {
                var to = _stageHome + axis * strength * (1 - i / (float)steps) * (i % 2 == 0 ? 1 : -1);
                tween.TweenProperty(_stage, "position", to, step);
            }
            tween.TweenProperty(_stage, "position", _stageHome, step);
        }
    }
}
