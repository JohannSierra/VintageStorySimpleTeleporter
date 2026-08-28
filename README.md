# Vintage Story Simple Teleporter

Un mod para [Vintage Story](https://www.vintagestory.at/) que permite a los jugadores en modo *survival* teletransportarse a sus puntos de control (waypoints) elegidos en el mapa, con un costo económico balanceado utilizando Engranes.

## Características
- **Interfaz Integrada:** Simplemente abre el mapa (`M`), posa tu cursor sobre el ícono de un waypoint, y presiona la tecla **T**.
- **Menú de Selección:** Aparecerá un Pop-Up integrado nativamente al juego que te permitirá elegir el método de pago.
- **Costos y Tiempos Balanceados:**
  - Pagar con **1 Engrane Temporal**: Teletransporte tras 10 segundos de canalización.
  - Pagar con **25 Engranes Oxidados**: Teletransporte tras 20 segundos de canalización.
- **Prevención de Trampas:** El mod deduce el costo exactamente antes de teletransportar. Si el jugador tira, transfiere o guarda los engranes durante los 10/20 segundos de espera, el teletransporte se cancela.

## Instalación
1. Compila el proyecto usando `dotnet build`.
2. El archivo `WaypointTeleportMod.dll` y `modinfo.json` se guardarán automáticamente en la carpeta de Mods.
3. Activa "Waypoint Teleport (Engranes)" en el Mod Manager de Vintage Story.

## Tecnologías
- C# (.NET 10)
- API Cliente/Servidor de Vintage Story v1.22+
