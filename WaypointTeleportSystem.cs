using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace WaypointTeleport
{
    // 1. Definimos el Paquete de Red
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TeleportRequestPacket
    {
        public double X;
        public double Y;
        public double Z;
        public string WaypointName;
        public string SelectedGearType; 
    }

    // 2. Interfaz de Confirmación (El Pop-up de pago)
    public class TeleportConfirmDialog : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        private string waypointName;
        private Action<string> onConfirm;

        public TeleportConfirmDialog(ICoreClientAPI capi, string wpName, Action<string> onConfirm) : base(capi)
        {
            this.waypointName = wpName;
            this.onConfirm = onConfirm;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 350, 40);
            
            ElementBounds buttonTemporalBounds = ElementBounds.Fixed(0, 90, 350, 30);
            ElementBounds buttonRustyBounds = ElementBounds.Fixed(0, 130, 350, 30);
            ElementBounds buttonCancelBounds = ElementBounds.Fixed(0, 180, 350, 30);

            SingleComposer = capi.Gui.CreateCompo("tpconfirm", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Método de Viaje", () => TryClose())
                .AddDynamicText($"Viajar a '{waypointName}'. Elige tu forma de pago:", CairoFont.WhiteSmallText(), textBounds, "text")
                
                .AddSmallButton("Pagar 1 Engrane Temporal (Espera 10s)", OnUseTemporal, buttonTemporalBounds)
                .AddSmallButton("Pagar 25 Engranes Oxidados (Espera 20s)", OnUseRusty, buttonRustyBounds)
                .AddSmallButton("Cancelar", OnNo, buttonCancelBounds)
                .Compose();
        }

        private bool OnUseTemporal() { onConfirm?.Invoke("gear-temporal"); TryClose(); return true; }
        private bool OnUseRusty() { onConfirm?.Invoke("gear-rusty"); TryClose(); return true; }
        private bool OnNo() { TryClose(); return true; }
    }

    // 3. NUEVA Interfaz: Lista de Waypoints 
    public class WaypointListDialog : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        private IList waypoints;
        private Action<dynamic> onWaypointSelected;
        private int currentPage = 0;
        private const int itemsPerPage = 8; // Muestra 8 waypoints por página para no salir de la pantalla

        public WaypointListDialog(ICoreClientAPI capi, IList waypoints, Action<dynamic> onWaypointSelected) : base(capi)
        {
            this.waypoints = waypoints ?? new List<object>();
            this.onWaypointSelected = onWaypointSelected;
            SetupDialog();
        }

        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui.CreateCompo("wplist", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Mis Waypoints", () => TryClose())
                .AddDynamicText("Selecciona tu destino:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 30, 400, 30), "text");

            int yOffset = 70;
            int start = currentPage * itemsPerPage;
            int end = Math.Min(start + itemsPerPage, waypoints.Count);

            for (int i = start; i < end; i++)
            {
                dynamic wp = waypoints[i];
                string title = wp.Title ?? "Waypoint " + i;
                dynamic wpRef = wp;
                
                composer.AddSmallButton(title, () => OnWaypointClick(wpRef), ElementBounds.Fixed(0, yOffset, 400, 30), "btn_" + i);
                yOffset += 40;
            }

            if (currentPage > 0)
            {
                composer.AddSmallButton("< Anterior", OnPrev, ElementBounds.Fixed(0, yOffset, 150, 30));
            }
            if (end < waypoints.Count)
            {
                composer.AddSmallButton("Siguiente >", OnNext, ElementBounds.Fixed(250, yOffset, 150, 30));
            }
            if (waypoints.Count == 0)
            {
                composer.AddDynamicText("No tienes ningún waypoint guardado.", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, yOffset, 400, 30), "notfound");
            }

            SingleComposer = composer.Compose();
        }

        private bool OnPrev() { currentPage--; SetupDialog(); return true; }
        private bool OnNext() { currentPage++; SetupDialog(); return true; }

        private bool OnWaypointClick(dynamic wp)
        {
            onWaypointSelected?.Invoke(wp);
            TryClose();
            return true;
        }
    }

    // 4. Sistema Central del Mod
    public class WaypointTeleportSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private IServerNetworkChannel sChannel;
        private IClientNetworkChannel cChannel;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.Network.RegisterChannel("waypointteleport")
                .RegisterMessageType<TeleportRequestPacket>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            sChannel = api.Network.GetChannel("waypointteleport");
            sChannel.SetMessageHandler<TeleportRequestPacket>(OnTeleportRequest);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            cChannel = api.Network.GetChannel("waypointteleport");
            
            // Registramos el atajo con ctrlPressed = true
            api.Input.RegisterHotKey("tpwaypoint", "Menú de Viaje a Waypoint (Ctrl+T)", GlKeys.T, HotkeyType.CharacterControls, false, true, false);
            api.Input.SetHotKeyHandler("tpwaypoint", OnTeleportHotkey);
        }

        private bool OnTeleportHotkey(KeyCombination t1)
        {
            var mapManager = capi.ModLoader.GetModSystem<Vintagestory.GameContent.WorldMapManager>();
            if (mapManager == null) 
            {
                capi.ShowChatMessage("No se pudo acceder al mapa.");
                return true;
            }

            var wpLayer = mapManager.MapLayers.FirstOrDefault(l => l.GetType().Name == "WaypointMapLayer");
            if (wpLayer == null) 
            {
                capi.ShowChatMessage("Error: No se encontró la capa de waypoints.");
                return true; 
            }

            IList waypointsList = null;
            try 
            {
                // Buscar la lista de waypoints dinámicamente
                var prop = wpLayer.GetType().GetProperty("Waypoints", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null) waypointsList = prop.GetValue(wpLayer) as IList;
                
                if (waypointsList == null) 
                {
                    var field = wpLayer.GetType().GetField("ownWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null) waypointsList = field.GetValue(wpLayer) as IList;
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Error("Error buscando waypoints: " + ex);
            }

            if (waypointsList != null)
            {
                // Abrimos el NUEVO menú de lista en lugar de comprobar el ratón
                var dialog = new WaypointListDialog(capi, waypointsList, (wp) => {
                    string title = wp.Title ?? "Waypoint";
                    
                    var confirmDialog = new TeleportConfirmDialog(capi, title, (selectedGear) => {
                        cChannel.SendPacket(new TeleportRequestPacket {
                            X = wp.Position.X,
                            Y = wp.Position.Y,
                            Z = wp.Position.Z,
                            WaypointName = title,
                            SelectedGearType = selectedGear
                        });
                    });
                    
                    confirmDialog.TryOpen();
                });
                
                dialog.TryOpen();
            }
            else
            {
                capi.ShowChatMessage("No se pudo obtener la lista de waypoints.");
            }
            
            return true; 
        }

        private void OnTeleportRequest(IServerPlayer player, TeleportRequestPacket packet)
        {
            int delayMs = 0;
            int amountNeeded = 0;
            string itemCode = packet.SelectedGearType;

            if (itemCode == "gear-temporal")
            {
                amountNeeded = 1;
                delayMs = 10000;
            }
            else if (itemCode == "gear-rusty")
            {
                amountNeeded = 25;
                delayMs = 20000;
            }
            else return;

            if (HasItem(player, itemCode, amountNeeded))
            {
                string nameDisplay = itemCode == "gear-temporal" ? "engrane temporal" : "engranes oxidados";
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Preparando {nameDisplay}... Viaje en {delayMs/1000} segundos. ¡Prepárate!", EnumChatType.Notification);
            }
            else
            {
                string displayAmount = itemCode == "gear-temporal" ? "1 Temporal" : "25 Oxidados";
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"No tienes suficientes fondos para el método elegido (Requiere {displayAmount}).", EnumChatType.CommandError);
                return;
            }

            sapi.Event.RegisterCallback((dt) => {
                if (TryConsumeItem(player, itemCode, amountNeeded))
                {
                    player.Entity.TeleportToDouble(packet.X, player.Entity.Pos.Y, packet.Z);
                    player.SendMessage(GlobalConstants.GeneralChatGroup, $"Viaje temporal exitoso hacia {packet.WaypointName}.", EnumChatType.Notification);
                }
                else
                {
                    player.SendMessage(GlobalConstants.GeneralChatGroup, "El viaje falló: perdiste o moviste los engranes durante la preparación.", EnumChatType.CommandError);
                }
            }, delayMs);
        }

        private bool HasItem(IServerPlayer player, string itemCode, int amountNeeded)
        {
            int amountFound = 0;
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inventory)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Collectible.Code.Path == itemCode) amountFound += slot.StackSize;
                }
            }
            return amountFound >= amountNeeded;
        }

        private bool TryConsumeItem(IServerPlayer player, string itemCode, int amountNeeded)
        {
            int amountFound = 0;
            List<ItemSlot> slotsToConsume = new List<ItemSlot>();

            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                foreach (var slot in inventory)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Collectible.Code.Path == itemCode)
                    {
                        amountFound += slot.StackSize;
                        slotsToConsume.Add(slot);
                    }
                }
            }

            if (amountFound >= amountNeeded)
            {
                int amountRemainingToTake = amountNeeded;
                foreach (var slot in slotsToConsume)
                {
                    int taken = Math.Min(slot.StackSize, amountRemainingToTake);
                    slot.TakeOut(taken);
                    slot.MarkDirty();
                    
                    amountRemainingToTake -= taken;
                    if (amountRemainingToTake <= 0) break;
                }
                
                player.BroadcastPlayerData(true);
                return true;
            }
            return false;
        }
    }
}
