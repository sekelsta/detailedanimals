// TODO: Remove extra imports
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Genelib {
    public class GuiDialogAnimal : GuiDialog {
        protected EntityAgent animal;

        public GuiDialogAnimal(ICoreClientAPI capi, EntityAgent animal) : base(capi) {
            this.animal = animal;

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            string animalName = animal.GetBehavior<EntityBehaviorNameTag>().DisplayName;
            SingleComposer = capi.Gui.CreateCompo("animaldialog-" + animal.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(animalName, () => { TryClose(); } )
                //.AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont, tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                .BeginChildElements(bgBounds);
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
