using Harmony;
using KSerialization;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace HexiGeyserCracking {
    public static class HexiGeyserCrackingMod
    {
        public static void OnLoad()
        {
            Strings.Add(Crackable.TitleID, "Crack Natural Feature");
		}
	}

    [HarmonyPatch(typeof(Geyser), "OnCmpEnable")]
    class Hexi_Geyser_Patch_OnSpawn {
        static void Prefix(ref Geyser __instance) {
            if (__instance.GetType() == typeof(Geyser)) {
                __instance.FindOrAddComponent<Crackable>().geyser = __instance;

                // for the delivery - needs to be here for persistent storage
                Storage storage = __instance.FindOrAddComponent<Storage>();
                storage.allowItemRemoval = false;
                storage.capacityKg = Crackable.DELIVERY_REQUIRED;
                storage.showInUI = true;
            }
        }
    }

    public class Crackable : Workable, ISidescreenButtonControl, ISim1000ms {
        // TODO: consider making cracking an over-time operation instead, with X kg of sulfur adding Y% output per 1s of usage
        // TODO: consider removing the upper limit, with the operation efficiency based on the existing output (1/x perhaps)

        public Geyser geyser;

        private bool maxCracked = false;
        private Chore chore;

        [Serialize]
        private bool markedForCracking; // true if an errand is queued
        [Serialize]
        private float curP; // the current emission value, up to 4
        [Serialize]
        private bool removedStudyable; // true if the studyable component has been removed to allow this button to show up

        // the amount of sulfur required per operation, in kg
        public const float DELIVERY_REQUIRED = 100;

        // the amount of additional output per operation, as a proportion of the max output
        public const float BOOST_MIN = 0.15f;
        public const float BOOST_MAX = 0.3f;
        
		public static string TitleID = "UI.UISIDESCREENS.HEXICRACKING.TITLE";
        public string SidescreenTitleKey {
            get {
                return TitleID;
            }
        }

        public string SidescreenStatusMessage {
            get {
                if (maxCracked) return "Researchers have improved this natural feature as much as possible.";
                if (markedForCracking) return "A researcher is improving this natural feature.";
                return "Send a researcher to improve this natural feature.";
            }
        }

        public string SidescreenButtonText {
            get {
                if (maxCracked) return "CRACKING COMPLETE";
                if (markedForCracking) return "CANCEL CRACKING";
                return "PERFORM CRACKING";
            }
        }

        public void OnSidescreenButtonPressed() {
            if (maxCracked) return;
            if (DebugHandler.InstantBuildMode) {
                if (chore != null) {
                    chore.Cancel("debug");
                    OnCracked(chore);
                    chore = null;
                }
                else OnCracked(null);
            }
            else {
                markedForCracking = !markedForCracking;
                Sim1000ms(0);
            }
        }

        protected override void OnPrefabInit() {
            base.OnPrefabInit();
            this.overrideAnims = new KAnimFile[]
            {
                Assets.GetAnim("anim_use_machine_kanim")
            };
            this.faceTargetWhenWorking = true;
            this.synchronizeAnims = false;
            this.workerStatusItem = Db.Get().DuplicantStatusItems.Studying;
            this.resetProgressOnStop = false;
            this.requiredSkillPerk = Db.Get().SkillPerks.CanStudyWorldObjects.Id;
            this.attributeConverter = Db.Get().AttributeConverters.ResearchSpeed;
            this.attributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.MOST_DAY_EXPERIENCE;
            this.skillExperienceSkillGroup = Db.Get().SkillGroups.Research.Id;
            this.skillExperienceMultiplier = SKILLS.MOST_DAY_EXPERIENCE;
            this.SetWorkTime(120f);
        }

        protected override void OnSpawn() {
            base.OnSpawn();

            // Debug.Log("Marked for cracking: " + markedForCracking);
            // Debug.Log("Current percentage: " + curP);
            // Debug.Log("Removed Studyable: " + removedStudyable);

            // Debug.Log("Geyser: " + geyser);
            GeyserConfigurator.GeyserInstanceConfiguration conf = geyser.configuration;
            // Debug.Log("Config: " + conf);
            float maxOut = conf.geyserType.maxRatePerCycle;
            float curOut = conf.GetMassPerCycle();
            if (curP <= 0) curP = curOut / maxOut;
            else SetStats(maxOut * curP);
            Invoke("CheckStudyable", 1f);
            Sim1000ms(0);
        }

        private static FieldInfo scaledRate = typeof(GeyserConfigurator.GeyserInstanceConfiguration).GetField("scaledRate", BindingFlags.Instance | BindingFlags.NonPublic);
        // private static FieldInfo scaledIter = typeof(GeyserConfigurator.GeyserInstanceConfiguration).GetField("scaledIterationPercent", BindingFlags.Instance | BindingFlags.NonPublic);
        // private static FieldInfo scaledYear = typeof(GeyserConfigurator.GeyserInstanceConfiguration).GetField("scaledYearPercent", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo emitter = typeof(Geyser).GetField("emitter", BindingFlags.Instance | BindingFlags.NonPublic);

        private void SetStats(float output) {
            scaledRate.SetValue(geyser.configuration, output);
            ((ElementEmitter)emitter.GetValue(geyser)).outputElement.massGenerationRate = geyser.configuration.GetEmitRate();
        }

        public void OnCracked(Chore chore) {
            // Debug.Log("Cracking complete for geyser: " + geyser.ToString());
            GeyserConfigurator.GeyserInstanceConfiguration conf = geyser.configuration;
            // Debug.Log("Config: " + conf.ToString());

            // boost rate
            // Debug.Log("Type: " + conf.geyserType.ToString());
            float maxOut = conf.geyserType.maxRatePerCycle;
            float curOut = conf.GetMassPerCycle();
            // Debug.Log("Performing cracking...");
            if (curOut < maxOut * 4) { // can go up to 4x max out
                float rem = (maxOut * 4) - curOut;
                float remP = rem / maxOut;
                // Debug.Log("remP: " + remP);
                // add a random boost
                float boostAmt = Random.Range(BOOST_MIN, BOOST_MAX);
                // Debug.Log("Boosting: " + boostAmt);
                if (boostAmt > remP - 0.001f) {
                    // max out
                    // Debug.Log("Boost will max out.");
                    maxCracked = true;
                    SetStats(maxOut * 4);
                }
                else {
                    // add random boost
                    // Debug.Log("Performing boost.");
                    SetStats(maxOut * (4 - remP + boostAmt));
                }
            }
            curP = conf.GetMassPerCycle() / maxOut;
            geyser.GetComponent<Storage>().items.RemoveAll(it => true);
            this.chore = null;
            markedForCracking = false;
            UpdateUI();

            // boost short timings
            // Debug.Log("Boosting short timing.");
            // if (conf.GetIterationPercent() < 1.0f) scaledIter.SetValue(conf, Random.Range(conf.GetIterationPercent(), 1.0f));

            // boost long timings
            // Debug.Log("Boosting long timing.");
            // if (conf.GetYearPercent() < 0.9f) scaledYear.SetValue(conf, Random.Range(conf.GetYearPercent(), 0.9f));

            // TODO: forcibly start an eruption?
        }

        public void CheckStudyable() {
            if (!KMonoBehaviour.isLoadingScene) {
                Studyable s = geyser.GetComponent<Studyable>();
                if (removedStudyable || (s != null && s.Studied)) {
                    if (s != null) {
                        s.Refresh();
                        Object.Destroy(s);
                    }
                    removedStudyable = true;

                    // add delivery task
                    ManualDeliveryKG deliver = geyser.gameObject.AddOrGet<ManualDeliveryKG>();
                    deliver.SetStorage(geyser.GetComponent<Storage>());
                    deliver.requestedItemTag = ElementLoader.FindElementByName("Sulfur").tag;
                    deliver.refillMass = DELIVERY_REQUIRED;
                    deliver.capacity = DELIVERY_REQUIRED;
                    // deliver.choreTags = GameTags.ChoreTypes.ResearchChores;
                    deliver.choreTypeIDHash = Db.Get().ChoreTypes.ResearchFetch.IdHash;

                    UpdateUI();
                    return;
                }
            }
            Invoke("CheckStudyable", 1f);
        }
        
        private void UpdateUI() {
            if (DetailsScreen.Instance.target == geyser.gameObject && SelectTool.Instance.selected != null) {
                SelectTool.Instance.selected = geyser.GetComponent<KSelectable>(); // precaution
                DetailsScreen.Instance.Refresh(geyser.gameObject);
            }
        }

        public void Sim1000ms(float dt) {
            if (KMonoBehaviour.isLoadingScene) return;
            if (!geyser.FindComponent<ManualDeliveryKG>()) return;
            if (markedForCracking && !maxCracked) {
                geyser.GetComponent<ManualDeliveryKG>().Pause(false, "Cracking requested");
                if (chore == null) {
                    if (geyser.GetComponent<Storage>().MassStored() >= DELIVERY_REQUIRED) {
                        chore = new WorkChore<Crackable>(
							Db.Get().ChoreTypes.Research, this, null, true,
							new System.Action<Chore>(OnCracked), null, null, false,
							null, false, false, null, false, true, true,
							PriorityScreen.PriorityClass.basic, 5, false, false);
                    }
                }
            }
            else {
                if (maxCracked) {
                    geyser.GetComponent<ManualDeliveryKG>().Pause(true, "Cracking complete");
                    Object.Destroy(geyser.GetComponent<ManualDeliveryKG>());
                    Object.Destroy(geyser.GetComponent<Storage>());
                }
                else geyser.GetComponent<ManualDeliveryKG>().Pause(true, "Cracking cancelled");
                if (chore != null) {
                    chore.Cancel("Cancelled");
                    chore = null;
                }
            }
        }
    }
}
