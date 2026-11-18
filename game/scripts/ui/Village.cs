using System;
using System.Linq;
using Godot;
using YangVillage.Core;

// YangVil Unity 판 Village.cs 이식. screen_id = village
namespace YangVillage.Ui
{
    // 마을: 화면 전체가 마당(고양이 배회). 위에 재화·가이드, 아래에 메뉴 줄, 나머지는 메뉴에서 여는 창.
    public partial class Village : Control
    {
        const float CatSize = 240;
        const float MenuHeight = 104;
        const float IconSize = 52;
        Game G => App.Game;
        Control _yard, _popup;
        Label _coins, _treats, _guide;
        Button _hatcheryButton, _catsButton;
        Action _reopen;
        float _tick;
        bool _built;

        public override void _Ready()
        {
            Kit.Root(this, Kit.Grass);
            _yard = Kit.Add(this, new Control { Name = "yard", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore });
            Kit.Fill(_yard);

            var column = Kit.Column(this, 12, 8);

            var top = Kit.Panel(column, Kit.Alpha(Kit.Paper, 0.92f), Kit.Ink, 16, 8);
            var bar = Kit.Row(top, 10);
            SpriteView.Of("sprite_reward_coin", 36, bar);
            _coins = Kit.Label(bar, "", 22);
            _treats = Kit.Label(bar, "", 22);
            Kit.Expand(_coins);
            Kit.Expand(_treats);

            var guide = Kit.Panel(column, Kit.Alpha(Kit.Night, 0.88f), Kit.Gold, 14, 10);
            _guide = Kit.Label(guide, "", 18, Kit.Paper);

            Kit.Spacer(column);

            var menu = Kit.Row(column, 8);
            _hatcheryButton = MenuButton(menu, "sprite_basket", "ui_menu_hatchery", () => Open(Hatchery), false);
            MenuButton(menu, "sprite_reward_coin", "ui_menu_shop", () => Open(Shop), false);
            _catsButton = MenuButton(menu, "sprite_food_bowl", "ui_menu_cats", () => Open(Cats), false);
            MenuButton(menu, "sprite_pawprint", "ui_menu_adventure", () => App.Go("stage_select"), true);

            // 마당 크기가 정해진 뒤 첫 갱신.
            Callable.From(() =>
            {
                _built = true;
                Refresh();
            }).CallDeferred();
        }

        // 메뉴 칸: 그림 위, 이름 아래. 모험만 강조색.
        Button MenuButton(Node parent, string spriteId, string textId, Action onPress, bool primary)
        {
            var button = primary
                ? Kit.Button(parent, App.T(textId), onPress, 22)
                : Kit.Button(parent, App.T(textId), onPress, 18, Kit.Paper, Kit.Cream);
            Kit.ButtonIcon(button, Sprites.Frames(spriteId)[0], IconSize);
            button.CustomMinimumSize = new Vector2(0, MenuHeight);
            Kit.Expand(button, primary ? 1.3f : 1f);
            return button;
        }

        public override void _Process(double delta)
        {
            if (!_built) return;
            SortYard();
            _tick += (float)delta;
            if (_tick < 1) return;
            _tick = 0;
            Refresh(false);
        }

        // 발밑 y 순으로 형제 순서 정렬 (0번 = 바닥 그림).
        void SortYard()
        {
            var items = _yard.GetChildren().Skip(1).Cast<Control>()
                .Select(c => (c, y: c is CatWalker w ? w.SortY : c.Position.Y))
                .OrderBy(p => p.y).ToList();
            for (var i = 0; i < items.Count; i++)
                if (items[i].c.GetIndex() != i + 1) _yard.MoveChild(items[i].c, i + 1);
        }

        void Say(string textId, params object[] args)
        {
            Refresh();
            if (textId != null) _guide.Text = App.T(textId, args);
        }

        // 지금 할 일 한 줄.
        string Guide()
        {
            if (G.Data.baskets.Any(G.IsReady)) return "ui_guide_hatch";
            if (G.Data.cats.Count == 0) return G.Data.baskets.Count == 0 ? "ui_guide_basket" : "ui_guide_wait";
            return G.Data.cats.Any(c => c.inParty) ? "ui_guide_adventure" : "ui_guide_party";
        }

        void Refresh(bool yard = true)
        {
            _coins.Text = App.T("ui_coins", G.Data.coins);
            _treats.Text = App.T("ui_treats", G.Data.treats);
            _guide.Text = App.T(Guide());
            _hatcheryButton.Text = App.T("ui_menu_badge", App.T("ui_menu_hatchery"), G.Data.baskets.Count);
            _catsButton.Text = App.T("ui_menu_badge", App.T("ui_menu_cats"), G.Data.cats.Count);
            if (yard) RefreshYard();
            if (_reopen != null && (yard || _reopen == (Action)Hatchery)) Open(_reopen);
        }

        // 메뉴 창: 제목 + 내용 + 닫기. 내용이 바뀌면 같은 창을 다시 그린다.
        VBoxContainer Sheet(string title)
        {
            _popup?.QueueFree();
            _popup = Kit.Add(this, new Control { Name = "popup", MouseFilter = MouseFilterEnum.Ignore });
            Kit.Fill(_popup);
            Kit.Rect(_popup, Kit.Alpha(Kit.Night, 0.6f), true);
            var center = Kit.Add(_popup, new CenterContainer { MouseFilter = MouseFilterEnum.Ignore });
            Kit.Fill(center);
            var panel = Kit.Panel(center, Kit.Paper, Kit.Ink, 16);
            panel.CustomMinimumSize = new Vector2(460, 0);
            var body = Kit.VBox(panel, 10);
            if (title != null) Kit.Label(body, title, 24);
            return body;
        }

        void Close()
        {
            _popup?.QueueFree();
            _popup = null;
            _reopen = null;
        }

        void Open(Action sheet)
        {
            _reopen = sheet;
            sheet();
        }

        void Hatchery()
        {
            var body = Sheet(App.T("ui_hatchery", G.Data.baskets.Count, App.Db.ConfigInt("hatchery_slots")));
            if (G.Data.baskets.Count == 0) Kit.Label(body, App.T("ui_hatchery_empty"), 18, Kit.Muted);
            foreach (var basket in G.Data.baskets.ToList())
            {
                var row = Kit.Row(body);
                SpriteView.Of("sprite_basket", 64, row);
                var owned = basket;
                var left = basket.hatchAtUnix - G.Now;
                var label = Kit.Label(row, G.IsReady(basket) ? App.T("ui_basket_ready") : App.T("ui_basket_wait", $"{left / 60}:{left % 60:00}"), 18);
                Kit.Expand(label);
                if (G.IsReady(basket))
                {
                    var hatch = Kit.Button(row, App.T("ui_hatch"), () =>
                    {
                        var cat = G.Hatch(owned);
                        Say(cat != null ? "ui_hatched" : null, cat != null ? G.CatName(cat) : "");
                    }, 20);
                    hatch.SizeFlagsHorizontal = SizeFlags.Fill;
                }
            }
            Kit.Button(body, App.T("ui_buy_basket", App.Db.ConfigInt("basket_price")), () => Say(G.BuyBasket() ? null : "ui_cant_buy"));
            Kit.Button(body, App.T("ui_close"), Close);
        }

        void Shop()
        {
            var body = Sheet(App.T("ui_menu_shop"));
            Kit.Label(body, App.T("ui_coins", G.Data.coins) + "  ·  " + App.T("ui_treats", G.Data.treats), 18, Kit.AccentDark);
            Kit.Button(body, App.T("ui_buy_basket", App.Db.ConfigInt("basket_price")), () => Say(G.BuyBasket() ? null : "ui_cant_buy"));
            Kit.Button(body, App.T("ui_buy_treat", App.Db.ConfigInt("treat_price")), () => Say(G.BuyTreat() ? null : "ui_cant_buy"));
            Kit.Button(body, App.T("ui_close"), Close);
        }

        void Cats()
        {
            var body = Sheet(App.T("ui_cat_list") + "  ·  " + App.T("ui_cats", G.Data.cats.Count));
            var scroll = Kit.Add(body, new ScrollContainer { CustomMinimumSize = new Vector2(0, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
            var list = Kit.VBox(scroll, 8);
            list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            foreach (var cat in G.Data.cats.OrderByDescending(c => c.inParty).ThenByDescending(c => c.level))
            {
                var line = App.T("ui_cat_line", G.CatName(cat), cat.level, G.StageLabel(cat.level), G.ElementName(G.CatElement(cat)));
                var owned = cat;
                var button = Kit.Button(list, (cat.inParty ? App.T("ui_party_in") + "  " : "") + line, () => OpenCard(owned), 18);
                // 그림 = 글 왼쪽.
                Kit.ButtonIcon(button, Sprites.Frames(G.CatSprite(cat.catId))[0], 64, false);
                button.CustomMinimumSize = new Vector2(0, 72);
            }
            Kit.Button(body, App.T("ui_close"), Close);
        }

        void RefreshYard()
        {
            foreach (var child in _yard.GetChildren()) { _yard.RemoveChild(child); child.QueueFree(); }
            var area = _yard.Size;
            var ground = SpriteView.Of("sprite_village_courtyard", area.Y, _yard);
            Kit.Fill(ground);
            ground.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            Prop("sprite_food_bowl", 0.3f, 0.46f, 96);
            Prop("sprite_yarn_ball", 0.66f, 0.68f, 72);

            var bounds = new Rect2(area.X * 0.04f, area.Y * 0.34f, area.X * 0.92f, area.Y * 0.5f);
            foreach (var cat in G.Data.cats.OrderByDescending(c => c.inParty).Take(App.Db.ConfigInt("village_slots")))
            {
                var size = CatSize * App.Db.ConfigInt("growth_scale_pct_" + G.GrowthStage(cat.level)) / 100f;
                var owned = cat;
                var walker = CatWalker.Create(_yard, G.CatSprite(cat.catId), size, bounds);
                walker.Tapped = () => OpenCard(owned);
                walker.Place();
            }
        }

        void Prop(string spriteId, float x, float y, float size)
        {
            var prop = SpriteView.Of(spriteId, size, _yard);
            prop.Position = new Vector2(_yard.Size.X * x, _yard.Size.Y * y);
        }

        // 고양이 카드
        void OpenCard(OwnedCat cat)
        {
            _reopen = () => OpenCard(cat);
            var body = Sheet(null);

            var portrait = SpriteView.Of(G.CatSprite(cat.catId), 180, Kit.Row(body));
            var icon = Sprites.ElementIcon(G.CatElement(cat), 48);
            if (icon != null) portrait.AddChild(icon);
            Kit.Label(body, App.T("ui_cat_line", G.CatName(cat), cat.level, G.StageLabel(cat.level), G.ElementName(G.CatElement(cat))), 20);
            var need = G.ExpToNext(cat);
            Kit.Label(body, need > 0 ? App.T("ui_cat_exp", cat.exp, need) : App.T("ui_cat_max"), 16, Kit.Muted);
            var note = Kit.Label(body, App.T("ui_treats", G.Data.treats), 16, Kit.AccentDark);

            Kit.Button(body, App.T("ui_feed"), () =>
            {
                if (!G.Feed(cat)) { note.Text = App.T("ui_no_treats"); return; }
                Refresh();
            });
            Kit.Button(body, App.T(cat.inParty ? "ui_party_in" : "ui_party_out"), () =>
            {
                if (!G.ToggleParty(cat)) { note.Text = App.T("ui_party_full", G.PartySize); return; }
                Refresh();
            });
            Kit.Button(body, App.T("ui_close"), Close);
        }
    }
}
