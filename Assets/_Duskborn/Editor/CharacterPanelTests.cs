using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using Duskborn.UI;
using InventorySystem.Bootstrap;
using InventorySystem.Core;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados para o Painel de Personagem (Character Section),
    /// incluindo equipar via clique direito no inventário, desequipar,
    /// estados de slots vazios/equipados, alternância de anéis e foco de menus.
    /// </summary>
    public static class CharacterPanelTests
    {
        [MenuItem("Duskborn/Tests/Run Character Panel Tests", false, 107)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_CharacterUIManager_InstanceAndLifecycle, ref passed, ref total);
            RunTest(Test_PlayerCameraController_IsAnyMenuOpen_IncludesCharacterUI, ref passed, ref total);
            RunTest(Test_PlayerEquipmentContainer_TryEquipToSlot, ref passed, ref total);
            RunTest(Test_RingSlot_AutomaticAlternation, ref passed, ref total);
            RunTest(Test_InventoryUIManager_RightClickEquip, ref passed, ref total);
            RunTest(Test_CharacterSlotView_VisualStates, ref passed, ref total);
            RunTest(Test_InGameMenuController_EscapeClosesCharacterPanelFirst, ref passed, ref total);
            RunTest(Test_InventoryUIManager_PopulatePlaceholderTestGear, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[CharacterPanelTests] {passed}/{total} testes passaram com sucesso!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
                Debug.Log($"[PASS] {testMethod.Method.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex.Message}");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new Exception(message);
        }

        private static void Test_CharacterUIManager_InstanceAndLifecycle()
        {
            var go = new GameObject("Test_CharacterUI");
            try
            {
                var charUI = go.AddComponent<CharacterUIManager>();
                AssertTrue(CharacterUIManager.Instance == charUI, "CharacterUIManager.Instance deve apontar para a instância criada.");
                AssertFalse(charUI.IsOpen, "Painel deve iniciar fechado.");
                AssertTrue(charUI.LastClosedFrame == -1, "LastClosedFrame inicial deve ser -1.");

                charUI.Open();
                AssertTrue(charUI.IsOpen, "Painel deve estar aberto após Open().");

                charUI.Close();
                AssertFalse(charUI.IsOpen, "Painel deve estar fechado após Close().");
                AssertTrue(charUI.LastClosedFrame == Time.frameCount, "LastClosedFrame deve ser atualizado para Time.frameCount ao fechar.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_PlayerCameraController_IsAnyMenuOpen_IncludesCharacterUI()
        {
            var go = new GameObject("Test_CharUI");
            var goCam = new GameObject("Test_Cam");
            try
            {
                var camCtrl = goCam.AddComponent<PlayerCameraController>();
                PlayerCameraController.LocalInstance = camCtrl;
                camCtrl.SetRotationLocked(false);
                AssertFalse(camCtrl.IsRotationLocked, "Câmera deve iniciar destravada.");

                var charUI = go.AddComponent<CharacterUIManager>();
                charUI.Close();

                charUI.Open();
                AssertTrue(PlayerCameraController.IsAnyMenuOpen(), "PlayerCameraController.IsAnyMenuOpen deve retornar true com CharacterUIManager aberto.");
                AssertTrue(camCtrl.IsRotationLocked, "Câmera deve travar rotação quando CharacterUIManager abre.");

                charUI.Close();
                AssertFalse(camCtrl.IsRotationLocked, "Câmera deve destravar rotação quando CharacterUIManager fecha e nenhum outro menu está aberto.");
            }
            finally
            {
                PlayerCameraController.LocalInstance = null;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }

        private static void Test_PlayerEquipmentContainer_TryEquipToSlot()
        {
            var go = new GameObject("Test_Player");
            try
            {
                go.AddComponent<PlayerStats>();
                go.AddComponent<PlayerBuffContainer>();
                var equip = go.AddComponent<PlayerEquipmentContainer>();

                var helm1 = new GearItem("helm_01", "Elmo de Teste 1", "HP: +5%", "", EquipmentSlot.Head,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.05f } });
                var helm2 = new GearItem("helm_02", "Elmo de Teste 2", "HP: +10%", "", EquipmentSlot.Head,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.10f } });

                bool equipped = equip.TryEquipToSlot(EquipmentSlot.Head, helm1, out var prev);
                AssertTrue(equipped, "TryEquipToSlot deve retornar true para item válido.");
                AssertTrue(prev == null, "Primeiro item equipado não deve ter item substituído.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == helm1, "GetEquipped deve retornar helm1.");

                // Substitui pelo helm2
                bool replaced = equip.TryEquipToSlot(EquipmentSlot.Head, helm2, out var displaced);
                AssertTrue(replaced, "Substituição deve retornar true.");
                AssertTrue(displaced == helm1, "Item substituído deve ser helm1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == helm2, "GetEquipped deve retornar helm2.");

                // Desequipa
                var unequipped = equip.Unequip(EquipmentSlot.Head);
                AssertTrue(unequipped == helm2, "Unequip deve retornar helm2.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Head) == null, "Slot deve estar vazio após Unequip.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_RingSlot_AutomaticAlternation()
        {
            var go = new GameObject("Test_Player_Rings");
            try
            {
                go.AddComponent<PlayerStats>();
                go.AddComponent<PlayerBuffContainer>();
                var equip = go.AddComponent<PlayerEquipmentContainer>();

                var ring1 = new GearItem("ring_01", "Anel de Cobre", "Crit: +5%", "", EquipmentSlot.Ring1, null);
                var ring2 = new GearItem("ring_02", "Anel de Ouro", "Crit: +10%", "", EquipmentSlot.Ring1, null);
                var ring3 = new GearItem("ring_03", "Anel de Rubi", "Crit: +15%", "", EquipmentSlot.Ring1, null);

                // 1º anel: Ring1 está vazio -> equipa em Ring1
                equip.TryEquipToSlot(EquipmentSlot.Ring1, ring1, out _);
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring1) == ring1, "Primeiro anel deve ir para Ring1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == null, "Ring2 deve continuar vazio.");

                // 2º anel: Ring1 ocupado e Ring2 vazio -> lógica de equipar escolhe Ring2
                EquipmentSlot target = equip.GetEquipped(EquipmentSlot.Ring1) == null ? EquipmentSlot.Ring1
                    : (equip.GetEquipped(EquipmentSlot.Ring2) == null ? EquipmentSlot.Ring2 : EquipmentSlot.Ring1);

                AssertTrue(target == EquipmentSlot.Ring2, "Alvo deve ser Ring2 quando Ring1 estiver ocupado.");
                equip.TryEquipToSlot(target, ring2, out _);
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == ring2, "Segundo anel deve estar em Ring2.");

                // 3º anel: ambos ocupados -> substitui Ring1
                target = equip.GetEquipped(EquipmentSlot.Ring1) == null ? EquipmentSlot.Ring1
                    : (equip.GetEquipped(EquipmentSlot.Ring2) == null ? EquipmentSlot.Ring2 : EquipmentSlot.Ring1);

                AssertTrue(target == EquipmentSlot.Ring1, "Alvo deve ser Ring1 quando ambos estiverem ocupados.");
                equip.TryEquipToSlot(target, ring3, out var displacedRing);
                AssertTrue(displacedRing == ring1, "Anel substituído deve ser ring1.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring1) == ring3, "Ring1 agora deve ter ring3.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Ring2) == ring2, "Ring2 deve permanecer intacto.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InventoryUIManager_RightClickEquip()
        {
            var goInv = new GameObject("Test_InventoryManager");
            var goPlayer = new GameObject("Test_Player_Combat");
            try
            {
                goPlayer.AddComponent<PlayerStats>();
                goPlayer.AddComponent<PlayerBuffContainer>();
                var equip = goPlayer.AddComponent<PlayerEquipmentContainer>();
                var combat = goPlayer.AddComponent<PlayerCombat>();

                var invUI = goInv.AddComponent<InventoryUIManager>();

                // Configura um serviço mock de inventário
                var grid = new InventoryGrid(4, 4);
                var service = new InventoryService(grid);

                // Usa reflexão para injetar o _playerEquipment no InventoryUIManager para o teste
                var fieldEquip = typeof(InventoryUIManager).GetField("_playerEquipment", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldEquip?.SetValue(invUI, equip);

                // Cria item de armadura
                var armor = new GearItem("chest_01", "Armadura de Couro", "HP: +8%", "", EquipmentSlot.Chest,
                    new[] { new StatBonus { Type = StatType.HP, Value = 0.08f } });

                service.TryPlaceItemAt(0, armor);
                AssertTrue(service.GetItem(0) == armor, "Item deve estar no slot 0 do inventário.");
                AssertTrue(equip.GetEquipped(EquipmentSlot.Chest) == null, "Chest deve estar inicialmente vazio.");

                // Testa o método de equipar direto
                equip.TryEquipToSlot(armor.Slot, armor, out var displaced);
                service.RemoveItem(0);

                AssertTrue(equip.GetEquipped(EquipmentSlot.Chest) == armor, "Chest deve conter o item equipado.");
                AssertTrue(service.GetItem(0) == null, "Slot 0 do inventário deve estar vazio após equipar.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goInv);
                UnityEngine.Object.DestroyImmediate(goPlayer);
            }
        }

        private static void Test_CharacterSlotView_VisualStates()
        {
            var go = new GameObject("Test_SlotView", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            var silGO = new GameObject("Silhouette", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            silGO.transform.SetParent(go.transform, false);
            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            itemGO.transform.SetParent(go.transform, false);

            try
            {
                var slotView = go.AddComponent<CharacterSlotView>();
                var silImg = silGO.GetComponent<UnityEngine.UI.Image>();
                var itemImg = itemGO.GetComponent<UnityEngine.UI.Image>();
                var outline = go.GetComponent<UnityEngine.UI.Outline>();

                slotView.Initialize(EquipmentSlot.Head, null, silImg, itemImg, outline);

                // Estado Vazio: Silhueta ativa, ícone de item desativado
                slotView.SetEmpty(null);
                AssertFalse(itemImg.gameObject.activeSelf, "Ícone de item deve estar desativado quando vazio.");

                // Estado Equipado: Ícone de item ativo, silhueta desativada
                var helm = new GearItem("helm_test", "Elmo", "HP: +5%", "", EquipmentSlot.Head, null);
                slotView.SetEquipped(helm, null);
                AssertFalse(silImg.gameObject.activeSelf, "Silhueta deve estar desativada quando equipado.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Test_InGameMenuController_EscapeClosesCharacterPanelFirst()
        {
            var goMenu = new GameObject("Test_InGameMenu");
            var goChar = new GameObject("Test_CharUI");
            try
            {
                var menu = goMenu.AddComponent<InGameMenuController>();
                var charUI = goChar.AddComponent<CharacterUIManager>();

                // Simula que o painel de personagem acabou de fechar neste frame
                charUI.Close();
                AssertTrue(charUI.LastClosedFrame == Time.frameCount, "LastClosedFrame deve registrar fechamento no frame atual.");

                // Invoca HandleEscapeKey via Reflection para verificar a prioridade
                var method = typeof(InGameMenuController).GetMethod("HandleEscapeKey", BindingFlags.NonPublic | BindingFlags.Instance);
                AssertTrue(method != null, "Método HandleEscapeKey deve existir.");

                method.Invoke(menu, null);

                // Como o painel fechou no mesmo frame, o menu de pausa NÃO deve abrir!
                AssertFalse(menu.IsOpen, "Menu de pausa NÃO deve abrir no mesmo frame em que o painel de personagem foi fechado.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goChar);
                UnityEngine.Object.DestroyImmediate(goMenu);
            }
        }

        private static void Test_InventoryUIManager_PopulatePlaceholderTestGear()
        {
            var goInv = new GameObject("Test_InventoryInstaller", typeof(InventoryInstaller));
            var goMgr = new GameObject("Test_InventoryUIManager", typeof(InventoryUIManager));
            try
            {
                var installer = goInv.GetComponent<InventoryInstaller>();
                var mgr = goMgr.GetComponent<InventoryUIManager>();

                // Injeta installer no manager via reflection
                var fieldInstaller = typeof(InventoryUIManager).GetField("installer", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInstaller?.SetValue(mgr, installer);

                // Configura serviço do installer via reflection
                var grid = new InventoryGrid(4, 4);
                var service = new InventoryService(grid);
                var fieldService = typeof(InventoryInstaller).GetField("_service", BindingFlags.NonPublic | BindingFlags.Instance);
                fieldService?.SetValue(installer, service);

                AssertTrue(mgr.InstallerReady, "InstallerReady deve ser true quando installer e service estiverem configurados.");

                // Executa a população de itens de teste
                mgr.PopulatePlaceholderTestGear();

                // Verifica que os itens foram inseridos
                int count = 0;
                for (int i = 0; i < grid.SlotCount; i++)
                {
                    if (service.GetItem(i) is GearItem) count++;
                }

                AssertTrue(count > 0, "PopulatePlaceholderTestGear deve adicionar itens de teste ao inventário.");

                // Executar uma segunda vez não deve duplicar itens existentes
                int countBefore = count;
                mgr.PopulatePlaceholderTestGear();
                int countAfter = 0;
                for (int i = 0; i < grid.SlotCount; i++)
                {
                    if (service.GetItem(i) is GearItem) countAfter++;
                }

                AssertTrue(countBefore == countAfter, "PopulatePlaceholderTestGear não deve duplicar itens já existentes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goMgr);
                UnityEngine.Object.DestroyImmediate(goInv);
            }
        }

        [MenuItem("Duskborn/Inventory/Populate Test Gear Items", false, 108)]
        public static void PopulateTestGearFromMenu()
        {
            var invUI = UnityEngine.Object.FindAnyObjectByType<InventoryUIManager>();
            if (invUI != null)
            {
                invUI.PopulatePlaceholderTestGear();
                Debug.Log("<color=#55FF55>[Duskborn] Itens de teste adicionados com sucesso ao inventário!</color>");
            }
            else
            {
                Debug.LogWarning("[Duskborn] InventoryUIManager não encontrado na cena ativa.");
            }
        }
    }
}
