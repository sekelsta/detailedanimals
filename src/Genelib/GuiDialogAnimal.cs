using System;

using Genelib.Extensions;
using Genelib.Network;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

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
                new GuiTab() { Name = Lang.Get("detailedanimals:gui-animalinfo-tab-status"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("detailedanimals:gui-animalinfo-tab-info"), DataInt = 1 },
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
            SingleComposer.EndChildElements().Compose();
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
                        SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-breedingprevented"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
                        y += 25;
                    }
                    else {
                        // TODO: Less hacky fix for crashing if the composer has no contents.
                        SingleComposer.AddStaticText(" ", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, width, 25));
                    }
                }
                else {
                    SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-preventbreeding"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, width, 25));
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
                    string n = Lang.Get("detailedanimals:gui-animalinfo-amount-" + nutrient.Amount);
                    string f = Lang.Get("detailedanimals:gui-animalinfo-amount-f-" + nutrient.Amount);
                    string m = Lang.Get("detailedanimals:gui-animalinfo-amount-m-" + nutrient.Amount);
                    string text = Lang.GetUnformatted("detailedanimals:gui-animalinfo-nutrient-" + nutrient.Name)
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

                string yearsKey = "detailedanimals:gui-age-year" + wholeYears;
                string stringYears = Lang.HasTranslation(yearsKey) ? Lang.Get(yearsKey) : Lang.Get("detailedanimals:gui-age-year", wholeYears);
                string monthsKey = "detailedanimals:gui-age-month" + wholeMonths;
                string stringMonths = Lang.HasTranslation(monthsKey) ? Lang.Get(monthsKey) : Lang.Get("detailedanimals:gui-age-month", wholeMonths);
                string daysKey = "detailedanimals:gui-age-day" + wholeDays;
                string stringDays = Lang.HasTranslation(daysKey) ? Lang.Get(daysKey) : Lang.Get("detailedanimals:gui-age-day", wholeDays);

                string ageString = stringYears + " " + stringMonths + " " + stringDays;
                string ageText = Lang.Get("detailedanimals:gui-animalinfo-age", ageString);
                SingleComposer.AddStaticText(ageText, infoFont, ElementBounds.Fixed(0, y, width, 25));
                y += 25;
            }

            string motherString = getParentName("mother");
            string fatherString = getParentName("father");
            SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-father", fatherString), infoFont, ElementBounds.Fixed(0, y, width, 25));
            y += 25;
            SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-mother", motherString), infoFont, ElementBounds.Fixed(0, y, width, 25));
            y += 25;
            if (animal.WatchedAttributes.HasAttribute("fosterId")) {
                long fosterId = animal.WatchedAttributes.GetLong("fosterId");
                if (fosterId != animal.WatchedAttributes.GetLong("motherId", -1)) {
                    string fosterString = getParentName("foster"); // TO_OPTIMIZE: skip getting foster ID again
                    SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-foster", fosterString), infoFont, ElementBounds.Fixed(0, y, width, 25));
                    y += 25;
                }
            }

            ITreeAttribute geneticsTree = animal.WatchedAttributes.GetTreeAttribute("genetics");
            if (geneticsTree != null && geneticsTree.HasAttribute("coi")) {
                float coi = geneticsTree.GetFloat("coi");
                if (coi >= 0.0395) {
                    string coiText = Lang.Get("detailedanimals:gui-animalinfo-inbreedingcoefficient", Math.Round(100 * coi));
                    SingleComposer.AddStaticText(coiText, infoFont, ElementBounds.Fixed(0, y, width, 25));
                    string desc = Lang.Get("detailedanimals:gui-animalinfo-inbreedingcoefficient-desc");
                    SingleComposer.AddAutoSizeHoverText(desc, CairoFont.WhiteDetailText(), 350, ElementBounds.Fixed(0, y, width, 25), "hoverCOI");
                    y += 25;
                }
            }

            SingleComposer.AddStaticText(Lang.Get("detailedanimals:gui-animalinfo-note"), infoFont, ElementBounds.Fixed(0, y, width, 25));
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

        // TO_OPTIMIZE: Consider calculating once on dialog open and caching the result, instead of recalculating every tick
        private string getParentName(string parent) {
            if (animal.WatchedAttributes.HasAttribute(parent+"Id")) {
                if (animal.WatchedAttributes.HasAttribute(parent+"Name")) {
                    return animal.WatchedAttributes.GetString(parent+"Name");
                }
                if (animal.WatchedAttributes.HasAttribute(parent+"Key")) {
                    return Lang.Get(animal.WatchedAttributes.GetString(parent+"Key"));
                }
                if (parent == "foster") {
                    return Lang.Get("detailedanimals:gui-animalinfo-unknownmother");
                }
                return Lang.Get("detailedanimals:gui-animalinfo-unknown" + parent);
            }
            else if (parent == "mother" && animal.WatchedAttributes.HasAttribute("fatherId")) {
                // For a time (until 0.3.2?) there was a bug where only fathers not mothers were being recorded
                return Lang.Get("detailedanimals:gui-animalinfo-unknownmother");
            }
            else {
                return Lang.Get("detailedanimals:gui-animalinfo-foundation");
            }
        }

        protected void OnTabClicked(int tab) {
            currentTab = tab;
            ComposeGui();
        }

        public override string ToggleKeyCombinationCode => null;
    }
}
