using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.UserInterface;
using System.Linq;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;



namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class FlynsarmyGuildServicePopupWindow : DaggerfallGuildServicePopupWindow
    {
        PlayerEntity playerEntity;
        GuildManager guildManager;
        int curingCost;
        FactionFile.GuildGroups guildGroup;
        StaticNPC serviceNPC;
        GuildNpcServices npcService;
        GuildServices currentService;
        int buildingFactionId;  // Needed for temples & orders
        IGuild guild;
        PlayerGPS.DiscoveredBuilding buildingDiscoveryData;
        bool isServiceWindowDeferred = false;

        public FlynsarmyGuildServicePopupWindow(IUserInterfaceManager uiManager, StaticNPC npc, FactionFile.GuildGroups guildGroup, int buildingFactionId)
            : base(uiManager, npc, guildGroup, buildingFactionId)
        {
            playerEntity = GameManager.Instance.PlayerEntity;
            guildManager = GameManager.Instance.GuildManager;

            serviceNPC = npc;
            npcService = (GuildNpcServices)npc.Data.factionID;
            currentService = Services.GetService(npcService);
            Debug.Log("NPC offers guild service: " + currentService.ToString());

            this.guildGroup = guildGroup;
            this.buildingFactionId = buildingFactionId;

            guild = guildManager.GetGuild(guildGroup, buildingFactionId);
        }

        public override void OnPush()
        {
            base.OnPush();

            buildingDiscoveryData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
        }

        protected override void Setup()
        {
            base.Setup();

            // We only want to add Cure Poison to merchants with Cure Disease service
            if (currentService.ToString() != "CureDisease")
            {
                return;
            }

            Debug.Log("GuildsCurePoison: Attempting to add buttons");

            // Get the panel just added - this is our service selection popup
            Panel mainPanel = (Panel)NativePanel.Components[NativePanel.Components.Count() - 1];
            // Now grab the 'Cure Disease' button
            Button cureButton = (Button)mainPanel.Components[mainPanel.Components.Count() - 2];

            // Halve the width of the Cure Disease button so we can slot our Cure Poison one in to the right
            cureButton.Size = new Vector2(cureButton.Size.x / 2, cureButton.Size.y);

            Rect serviceButtonRect = new Rect(
                cureButton.Position.x + cureButton.Size.x,
                cureButton.Position.y,
                cureButton.Size.x,
                cureButton.Size.y
            );
            Button serviceButton = new Button();
            TextLabel serviceLabel = new TextLabel();
            GuildServices service = Services.GetService((GuildNpcServices)FactionFile.FactionIDs.Temple_Healers);

            // Service button
            serviceLabel.Position = new Vector2(0, 1);
            serviceLabel.ShadowPosition = Vector2.zero;
            serviceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            serviceLabel.Text = "Cure Poison";
            Debug.Log(string.Format("GuildsCurePoison: New button text is {0}", serviceLabel.Text));
            serviceButton = DaggerfallUI.AddButton(serviceButtonRect, mainPanel);
            serviceButton.Components.Add(serviceLabel);
            serviceButton.OnMouseClick += CureDiseaseServiceButton_OnMouseClick;
            serviceButton.OnKeyboardEvent += ServiceButton_OnKeyboardEvent;
        }

        private void CureDiseaseServiceButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            CurePoisonService();
        }

        void ServiceButton_OnKeyboardEvent(BaseScreenComponent sender, Event keyboardEvent)
        {
            if (keyboardEvent.type == EventType.KeyDown)
            {
                DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
                isServiceWindowDeferred = true;
            }
            else if (keyboardEvent.type == EventType.KeyUp && isServiceWindowDeferred)
            {
                isServiceWindowDeferred = false;
                CurePoisonService();
            }
        }

        private void CureButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            CurePoisonService();
        }

        public void CurePoisonService()
        {
            Debug.Log("GuildsCurePoison: Custom Cure Poison service.");

            curingCost = 0;

            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            CloseWindow();
            int numberOfPoisons = GameManager.Instance.PlayerEffectManager.PoisonCount;

            // Check holidays for free / cheaper curing
            uint minutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            int holidayId = FormulaHelper.GetHolidayId(minutes, GameManager.Instance.PlayerGPS.CurrentRegionIndex);

            if (numberOfPoisons > 0 &&
                (holidayId == (int)DFLocation.Holidays.South_Winds_Prayer ||
                 holidayId == (int)DFLocation.Holidays.First_Harvest ||
                 holidayId == (int)DFLocation.Holidays.Second_Harvest))
            {
                GameManager.Instance.PlayerEffectManager.CureAllPoisons();
                DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("freeHolidayCuring"));
            }
            else if (numberOfPoisons > 0)
            {
                // Get base cost
                int baseCost = 250 * numberOfPoisons;

                // Apply rank-based discount if this is an Arkay temple
                baseCost = guild.ReducedCureCost(baseCost);

                // Apply temple quality and regional price modifiers
                int costBeforeBargaining = FormulaHelper.CalculateCost(baseCost, buildingDiscoveryData.quality);

                // Halve the price on North Winds Prayer holiday
                if (holidayId == (int)DFLocation.Holidays.North_Winds_Festival)
                    costBeforeBargaining /= 2;

                // Apply bargaining to get final price
                curingCost = FormulaHelper.CalculateTradePrice(costBeforeBargaining, buildingDiscoveryData.quality, false);

                // Index correct message
                const int tradeMessageBaseId = 260;
                int msgOffset = 0;
                if (costBeforeBargaining >> 1 <= curingCost)
                {
                    if (costBeforeBargaining - (costBeforeBargaining >> 2) <= curingCost)
                        msgOffset = 2;
                    else
                        msgOffset = 1;
                }

                Debug.Log(string.Format("GuildsCurePoison: Curing cost is {0}", curingCost));
                // Offer curing at the calculated price.
                DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
                TextFile.Token[] tokens = DaggerfallUnity.Instance.TextProvider.GetRandomTokens(tradeMessageBaseId + msgOffset);
                messageBox.SetTextTokens(tokens, this);
                messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
                messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
                messageBox.OnButtonClick += ConfirmPoisonCuring_OnButtonClick;
                messageBox.Show();
            }
            else
            {   // Not diseased
                DaggerfallUI.MessageBox(30);
            }
        }

        private void ConfirmPoisonCuring_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                if (playerEntity.GetGoldAmount() >= curingCost)
                {
                    playerEntity.DeductGoldAmount(curingCost);
                    GameManager.Instance.PlayerEffectManager.CureAllPoisons();
                    playerEntity.TimeToBecomeVampireOrWerebeast = 0;
                    DaggerfallUI.MessageBox(TextManager.Instance.GetLocalizedText("curedDisease"));
                }
                else
                    DaggerfallUI.MessageBox(NotEnoughGoldId);
            }
        }
    }
}
