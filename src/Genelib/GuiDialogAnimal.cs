using Genelib.Extensions;
using Genelib.Network;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

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
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-tab-status"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("genelib:gui-animalinfo-tab-info"), DataInt = 1 },
            };
            ElementBounds tabBounds = ElementBounds.Fixed(0, -24, width, 25);
            CairoFont tabFont = CairoFont.WhiteDetailText();

            string animalName = animal.GetBehavior<EntityBehaviorNameTag>().DisplayName;
            SingleComposer = capi.Gui.CreateCompo("animaldialog-" + animal.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar("", () => { TryClose(); } )
                .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont, tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                .BeginChildElements(bgBounds);

            if (animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                SingleComposer.AddStaticText(animalName, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, -14, 200, 22));
            }
            else {
                SingleComposer.AddTextInput(ElementBounds.Fixed(0, -14, 200, 22), OnNameSet, null, "animalName");
                SingleComposer.GetTextInput("animalName").SetValue(animalName);
            }

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

        private void OnNoteSet(string note) {
            SetNoteMessage message = new SetNoteMessage() { entityId = animal.EntityId, note = note };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<SetNoteMessage>(message);
        }

        private void OnPreventBreedingSet(bool value) {
            ToggleBreedingMessage message = new ToggleBreedingMessage() { entityId = animal.EntityId, preventBreeding = value };
            GenelibSystem.ClientAPI.Network.GetChannel("genelib").SendPacket<ToggleBreedingMessage>(message);
        }

        protected void AddStatusContents() {
            int y = 25;
            if (!animal.WatchedAttributes.GetBool("neutered", false)) {
                if (animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                    if (!animal.MatingAllowed()) {
                        SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-breedingprevented"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
                        y += 25;
                    }
                    else {
                        // TODO: Less hacky fix for crashing if the composer has no contents.
                        SingleComposer.AddStaticText(" ", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
                    }
                }
                else {
                    SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-preventbreeding"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, 25));
                    SingleComposer.AddSwitch(OnPreventBreedingSet, ElementBounds.Fixed(width - 25, y - 5, 25, 25), "preventbreeding");
                    SingleComposer.GetSwitch("preventbreeding").SetValue(!animal.MatingAllowed());
                    y += 25;
                }
            }
            AnimalHunger hunger = animal.GetBehavior<AnimalHunger>();
            if (hunger != null) {
                y += 5;
                SingleComposer.AddStaticText(Lang.Get("playerinfo-nutrition"), CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold), ElementBounds.Fixed(0, y, width, 25));
                y += 25;
                foreach (Nutrient nutrient in hunger.Nutrients) {
                    if (nutrient.Name == "water" || nutrient.Name == "minerals") {
                        // Don't display these until the player has a way to feed them
                        continue;
                    }
                    string n = Lang.Get("genelib:gui-animalinfo-amount-" + nutrient.Amount);
                    string f = Lang.Get("genelib:gui-animalinfo-amount-f-" + nutrient.Amount);
                    string m = Lang.Get("genelib:gui-animalinfo-amount-m-" + nutrient.Amount);
                    string text = Lang.GetUnformatted("genelib:gui-animalinfo-nutrient-" + nutrient.Name)
                        .Replace("{n}", n).Replace("{m}", m).Replace("{f}", f);
                    SingleComposer.AddStaticText(text, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
                    y += 20;
                }
                y += 5;
            }
        }

        protected void AddInfoContents() {
            CairoFont infoFont = CairoFont.WhiteDetailText();
            int y = 20;

            if (animal.WatchedAttributes.HasAttribute("birthTotalDays")) {
                double birthDate = animal.WatchedAttributes.GetDouble("birthTotalDays");
                double ageDays = animal.World.Calendar.TotalDays - birthDate;
                double ageMonths = ageDays / animal.World.Calendar.DaysPerMonth;
                double ageYears = ageMonths / 12;
                int wholeYears = (int) ageYears;
                double remainderMonths = ageMonths - wholeYears * 12;
                int wholeMonths = (int) remainderMonths;
                double remainderDays = ageDays - (wholeYears * 12 + wholeMonths) * animal.World.Calendar.DaysPerMonth;
                int wholeDays = (int) remainderDays;

                string yearsKey = "genelib:gui-age-year" + wholeYears;
                string stringYears = Lang.HasTranslation(yearsKey) ? Lang.Get(yearsKey) : Lang.Get("genelib:gui-age-year", wholeYears);
                string monthsKey = "genelib:gui-age-month" + wholeMonths;
                string stringMonths = Lang.HasTranslation(monthsKey) ? Lang.Get(monthsKey) : Lang.Get("genelib:gui-age-month", wholeMonths);
                string daysKey = "genelib:gui-age-day" + wholeDays;
                string stringDays = Lang.HasTranslation(daysKey) ? Lang.Get(daysKey) : Lang.Get("genelib:gui-age-day", wholeDays);

                string ageString = stringYears + " " + stringMonths + " " + stringDays;
                string ageText = Lang.Get("genelib:gui-animalinfo-age", ageString);
                SingleComposer.AddStaticText(ageText, infoFont, ElementBounds.Fixed(0, y, width, 25));
                y += 25;
            }
            SingleComposer.AddStaticText(Lang.Get("genelib:gui-animalinfo-note"), infoFont, ElementBounds.Fixed(0, y, width, 25));
            y += 25;
            string note = animal.GetBehavior<BehaviorAnimalInfo>().Note;
            if (animal.OwnedByOther((GenelibSystem.ClientAPI.World as ClientMain)?.Player)) {
                SingleComposer.AddStaticText(note, CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width - 20, 22));
            }
            else {
                SingleComposer.AddTextInput(ElementBounds.Fixed(0, y, width - 20, 22), OnNoteSet, CairoFont.WhiteDetailText(), "note");
                SingleComposer.GetTextInput("note").SetValue(note);
            }
            y += 25;
        }

        protected void OnTabClicked(int tab) {
            currentTab = tab;
            ComposeGui();
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
