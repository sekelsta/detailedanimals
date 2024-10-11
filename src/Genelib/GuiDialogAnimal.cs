using Genelib.Extensions;
using Genelib.Network;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Genelib {
    public class GuiDialogAnimal : GuiDialog {
        protected EntityAgent animal;
        protected int currentTab = 0;
        protected int width = 300;

        public GuiDialogAnimal(ICoreClientAPI capi, EntityAgent animal) : base(capi) {
            this.animal = animal;
            ComposeGui();
        }

        protected void ComposeGui() {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-status"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-info"), DataInt = 1 },
            };
            ElementBounds tabBounds = ElementBounds.Fixed(0, -24, width, 25);
            CairoFont tabFont = CairoFont.WhiteDetailText();

            string animalName = animal.GetBehavior<EntityBehaviorNameTag>().DisplayName;
            SingleComposer = capi.Gui.CreateCompo("animaldialog-" + animal.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("", () => { TryClose(); } )
                .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont, tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                .BeginChildElements(bgBounds);

            SingleComposer.AddTextInput(ElementBounds.Fixed(0, -14, 200, 22), OnNameSet, null, "animalName");
            SingleComposer.GetTextInput("animalName").SetValue(animalName);

            SingleComposer.GetHorizontalTabs("tabs").activeElement = currentTab;
            if (currentTab == 0) {
                AddStatusContents();
            }
            else if (currentTab == 1) {
                AddInfoContents();
            }
            SingleComposer.Compose();
        }

        private void OnNameSet(string name) {
            SetNameMessage message = new SetNameMessage() { entityId = animal.EntityId, name = name };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<SetNameMessage>(message);
        }

        protected void AddStatusContents() {
            int y = 20;
            // TODO: Nutrition, toggles
            SingleComposer.AddStaticText("TODO: Nutrition info, toggles", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
        }

        protected void AddInfoContents() {
            CairoFont infoFont = CairoFont.WhiteDetailText();
            int y = 20;
            // TODO
            SingleComposer.AddStaticText("Species: Bighorn Sheep", infoFont, ElementBounds.Fixed(0, y, width, 25));
            y += 25;
            SingleComposer.AddStaticText("Sex: Ewe", infoFont, ElementBounds.Fixed(0, y, width, 25));
            y += 25;
            SingleComposer.AddStaticText("Age: 2 months 5 days (lamb)", infoFont, ElementBounds.Fixed(0, y, width, 25));
        }

        protected void OnTabClicked(int tab) {
            currentTab = tab;
            ComposeGui();
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
