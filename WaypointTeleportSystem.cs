using System;
using System.Linq;
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
        // Nueva variable para decirle al servidor cómo elegimos pagar
        public string SelectedGearType; 
    }

    // 2. Definimos la interfaz (Pop-up con opciones múltiples)
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
            // Tamaño y alineación de la ventana
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            // Ajustamos el tamaño para acomodar botones en forma de lista
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

        private bool OnUseTemporal()
        {
            onConfirm?.Invoke("gear-temporal");
            TryClose();
            return true;
        }

        private bool OnUseRusty()
        {
            onConfirm?.Invoke("gear-rusty");
            TryClose();
            return true;
        }

        private bool OnNo()
        {
            TryClose();
            return true;
        }
    }

    // 3. Sistema Central del Mod
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
            
            api.Input.RegisterHotKey("tpwaypoint", "Teletransportarse al Waypoint (Requiere mapa abierto)", GlKeys.T, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("tpwaypoint", OnTeleportHotkey);
        }

        private bool OnTeleportHotkey(KeyCombination t1)
        {
            var mapManager = capi.ModLoader.GetModSystem<Vintagestory.GameContent.WorldMapManager>();
            if (mapManager == null) return false;

            if (!mapManager.IsOpened)
            {
                capi.ShowChatMessage("Debes tener el mapa abierto para usar el viaje temporal.");
                return false;
            }

            var wpLayer = mapManager.MapLayers.FirstOrDefault(l => l.GetType().Name == "WaypointMapLayer");
            if (wpLayer == null) return false;

            try 
            {
                object hoveredWp = null;
                var field = wpLayer.GetType().GetField("hoveredWaypoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    hoveredWp = field.GetValue(wpLayer);
                }

                if (hoveredWp != null)
                {
                    dynamic wp = hoveredWp;
                    string title = wp.Title ?? "Waypoint";
                    
                    // Mostrar Pop-up de selección de engranes
                    var dialog = new TeleportConfirmDialog(capi, title, (selectedGear) => {
                        cChannel.SendPacket(new TeleportRequestPacket {
                            X = wp.Position.X,
                            Y = wp.Position.Y,
                            Z = wp.Position.Z,
                            WaypointName = title,
                            SelectedGearType = selectedGear
                        });
                    });
                    
                    dialog.TryOpen();
                    return true;
                }
                else
                {
                    capi.ShowChatMessage("Coloca el cursor exactamente sobre el ícono de un waypoint y presiona T.");
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Error("Error al intentar teletransportarse al waypoint: " + ex);
                capi.ShowChatMessage("Hubo un error al leer el waypoint del mapa.");
            }
            
            return true; 
        }

        private void OnTeleportRequest(IServerPlayer player, TeleportRequestPacket packet)
        {
            int delayMs = 0;
            int amountNeeded = 0;
            string itemCode = packet.SelectedGearType;

            // Determinar costo según la selección del cliente
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
            else
            {
                return; // Paquete inválido
            }

            // Comprobar si el jugador tiene los fondos de la opción que eligió
            if (HasItem(player, itemCode, amountNeeded))
            {
                string nameDisplay = itemCode == "gear-temporal" ? "engrane temporal" : "engranes oxidados";
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"Preparando {nameDisplay}... Viaje en {delayMs/1000} segundos. ¡Prepárate!", EnumChatType.Notification);
            }
            else
            {
                // Si eligió una opción pero no tiene el dinero
                string displayAmount = itemCode == "gear-temporal" ? "1 Temporal" : "25 Oxidados";
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"No tienes suficientes fondos para el método elegido (Requiere {displayAmount}).", EnumChatType.CommandError);
                return;
            }

            // Registrar temporizador
            sapi.Event.RegisterCallback((dt) => {
                
                // Cobrar justo antes de saltar
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
                    if (slot.Itemstack.Collectible.Code.Path == itemCode)
                    {
                        amountFound += slot.StackSize;
                    }
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
