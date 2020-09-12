using Harmony;
using KSerialization;
using PeterHan.PLib.Options;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace HexiGeyserCracking {
    public static class HexiGeyserCrackingMod {
        public static void OnLoad() {
			POptions.RegisterOptions(typeof(ConfigData));
		}
	}

    [HarmonyPatch(typeof(Geyser), "OnCmpEnable")]
    class Hexi_Geyser_Patch_OnSpawn {
		private static List<Storage.StoredItemModifier> storageType = new List<Storage.StoredItemModifier>();
        static void Prefix(ref Geyser __instance) {
			if (storageType.Count == 0) {
				storageType.Add(Storage.StoredItemModifier.Insulate);
				storageType.Add(Storage.StoredItemModifier.Seal);
				storageType.Add(Storage.StoredItemModifier.Preserve);
			}
            if (__instance.GetType() == typeof(Geyser)) {
                __instance.FindOrAddComponent<Crackable>().geyser = __instance;

                // for the delivery - needs to be here for persistent storage
                Storage storage = __instance.FindOrAddComponent<Storage>();
                storage.allowItemRemoval = false;
                storage.capacityKg = POptions.SingletonOptions<ConfigData>.Instance.KgPerCrack;
                storage.showInUI = true;
				storage.SetDefaultStoredItemModifiers(storageType);
            }
        }
    }

	public class CrackableButton : KMonoBehaviour {
		public Crackable crackable;

		protected override void OnSpawn() {
			base.OnSpawn();
			Subscribe((int)GameHashes.RefreshUserMenu, OnUserMenuRefresh);
			Debug.Log($"[Geyser Cracking] Attached button: {crackable.maxCracked}/{crackable.markedForCracking}");
		}

		public void OnUserMenuRefresh(object obj) {
			string text = crackable.maxCracked ? "Cracking Complete" : (crackable.markedForCracking ? "Cancel Cracking" : "Start Cracking");
			string tooltip = crackable.maxCracked ? "This geyser cannot be improved further." : (crackable.markedForCracking ? "Cancel the existing job for improving this geyser." : "Start improving this geyser.");
			KIconButtonMenu.ButtonInfo info = new KIconButtonMenu.ButtonInfo(
				crackable.maxCracked ? "action_building_disabled" : "action_repair",
				text,
				OnToggleUserMenu,
				Action.NumActions,
				null,
				null,
				null,
				tooltip
			);
			Game.Instance.userMenu.AddButton(crackable.geyser.gameObject, info);
			Debug.Log($"[Geyser Cracking] Adding button: {text}");
		}

		public void OnToggleUserMenu() {
			if (!crackable.maxCracked) {
				if (DebugHandler.InstantBuildMode) {
					if (crackable.chore != null) {
						crackable.chore.Cancel("debug");
						crackable.OnCracked(crackable.chore);
						crackable.chore = null;
					}
					else crackable.OnCracked(null);
				}
				else {
					crackable.markedForCracking = !crackable.markedForCracking;
				}
			}
		}
	}

    public class Crackable : Workable, ISim1000ms {
        public Geyser geyser;
		public CrackableButton button;

        public bool maxCracked = false;
        public Chore chore;

        [Serialize]
        public bool markedForCracking; // true if an errand is queued
        [Serialize]
        public float curP; // the current emission value

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
			
            GeyserConfigurator.GeyserInstanceConfiguration conf = geyser.configuration;
            float maxOut = conf.geyserType.maxRatePerCycle;
            float curOut = GetStats();
            if (curP <= 0) curP = curOut / maxOut;
            else {
				SetStats(maxOut * curP);
				if (curP >= POptions.SingletonOptions<ConfigData>.Instance.MaxCracking) maxCracked = true;
			}
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

		private float GetStats() {
            GeyserConfigurator.GeyserInstanceConfiguration conf = geyser.configuration;
			return (float)scaledRate.GetValue(conf);
		}

        public void OnCracked(Chore chore) {
            GeyserConfigurator.GeyserInstanceConfiguration conf = geyser.configuration;

            // boost rate
            float maxOut = conf.geyserType.maxRatePerCycle;
            float curOut = GetStats();
            if (curOut < maxOut * POptions.SingletonOptions<ConfigData>.Instance.MaxCracking) {
                float rem = (maxOut * POptions.SingletonOptions<ConfigData>.Instance.MaxCracking) - curOut;
                float remP = rem / maxOut;
                float boostAmt = Random.Range(POptions.SingletonOptions<ConfigData>.Instance.MinPerCrack, POptions.SingletonOptions<ConfigData>.Instance.MaxPerCrack);
                if (boostAmt > remP - 0.001f) {
                    maxCracked = true;
                    SetStats(maxOut * POptions.SingletonOptions<ConfigData>.Instance.MaxCracking);
                }
                else {
                    SetStats(maxOut * (POptions.SingletonOptions<ConfigData>.Instance.MaxCracking - remP + boostAmt));
                }
            }
            curP = GetStats() / maxOut;
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
                if (s != null && s.Studied) {
                    if (s != null) s.Refresh();

                    // add delivery task
                    ManualDeliveryKG deliver = geyser.gameObject.AddOrGet<ManualDeliveryKG>();
                    deliver.SetStorage(geyser.GetComponent<Storage>());
                    deliver.requestedItemTag = ElementLoader.FindElementByName("Sulfur").tag;
                    deliver.refillMass = POptions.SingletonOptions<ConfigData>.Instance.KgPerCrack;
                    deliver.capacity = POptions.SingletonOptions<ConfigData>.Instance.KgPerCrack;
                    // deliver.choreTags = GameTags.ChoreTypes.ResearchChores;
                    deliver.choreTypeIDHash = Db.Get().ChoreTypes.ResearchFetch.IdHash;
					
					button = geyser.gameObject.AddOrGet<CrackableButton>();
					button.crackable = this;
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
			if (geyser.FindComponent<Storage>() == null || geyser.FindComponent<ManualDeliveryKG>() == null) return;
            if (!geyser.FindComponent<Storage>().showInUI) return;
            if (markedForCracking && !maxCracked) {
                geyser.GetComponent<ManualDeliveryKG>().Pause(false, "Cracking requested");
                if (chore == null) {
                    if (geyser.GetComponent<Storage>().MassStored() >= POptions.SingletonOptions<ConfigData>.Instance.KgPerCrack) {
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
					geyser.GetComponent<Storage>().showInUI = false;
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
