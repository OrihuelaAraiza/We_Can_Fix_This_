# We Can Fix This!

Juego cooperativo local de reparación para 1-4 jugadores. Los jugadores son la tripulación de una nave averiada: deben coordinar roles, reparar estaciones, sobrevivir fallos en cadena y derrotar al Core-X antes de que la integridad de la nave llegue a cero.

## Requisitos

- Unity `2022.3.62f3` LTS
- Universal Render Pipeline `14.0.12`
- New Input System `1.14.2`
- TextMeshPro

## Flujo de escenas

Las escenas activas en Build Settings son:

| Indice | Escena | Proposito |
|---|---|---|
| 0 | `00_Bootstrap` | Aplica settings globales y carga el menu principal. |
| 1 | `01_MainMenu scene` | Menu principal. |
| 2 | `02_Lobby` | Registro local de jugadores y seleccion de roles. |
| 3 | `03_Gameplay` | Partida principal. |

Los nombres oficiales viven en `Wcft.Core.GameConfig`. Para cambios de escena en scripts, usar `Wcft.Core.SceneLoader`.

## Controles

### Teclado

| Accion | P1 | P2 | P3 | P4 |
|---|---|---|---|---|
| Moverse | WASD | Flechas | TFGH | IJKL |
| Saltar | Espacio | Enter | R | P |
| Interactuar | E | Numpad 0 | Y | O |
| Habilidad | Q | Numpad 2 | T | U |

### Gamepad

| Accion | Boton |
|---|---|
| Moverse | Stick izquierdo |
| Saltar | A / Cruz |
| Interactuar | X / Cuadrado |
| Habilidad | Y / Triangulo |

## Roles

| Rol | Perk | Penalizacion |
|---|---|---|
| Fixie | Repara mas rapido | - |
| Comandante | Boost de velocidad al equipo | No puede reparar manualmente |
| Hacker | Reparacion remota | No puede reparar Energia |
| Mecanico | Resetea degradacion de estacion | - |
| Artillero | Torreta desde cualquier lugar | - |
| Saboteador | Bomba electrica contra NPCs | - |

## Setup rapido

1. Abrir el proyecto con Unity `2022.3.62f3`.
2. Abrir `Assets/Scenes/00_Bootstrap.unity` para probar el flujo completo.
3. Verificar que `EditorBuildSettings` contiene las cuatro escenas listadas arriba, en ese orden.
4. En `02_Lobby`, `LobbyManager` debe tener roles asignados en `availableRoles`.
5. En `03_Gameplay`, `PlayerManager` debe tener `PlayerData`, spawn/camera references y prefabs por slot si se usan.

## Arquitectura runtime

- `GameConfig` define nombres de escenas y settings globales.
- `SceneLoader` centraliza cambios de escena y recarga de escena activa.
- `LobbyPlayerSessionData` persiste dispositivos/control schemes del lobby al gameplay.
- `RoleSelectionData` persiste roles seleccionados entre lobby y gameplay.
- `RepairStation` mantiene una registry runtime (`ActiveStations`) y emite `OnStateChanged`.
- `ShipHealth` calcula el drenaje de nave desde la registry de estaciones, una sola vez por tick.
- `FailureSystem` mantiene sus eventos publicos (`OnStationFailed`, `OnStationRepaired`) como capa de compatibilidad para UI y sistemas existentes.

## Pruebas

Ejecutar EditMode tests desde Unity Test Runner:

```text
Window > General > Test Runner > EditMode > Run All
```

Tambien se pueden ejecutar por batchmode si Unity esta en PATH:

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditModeResults.xml -quit
```

Cobertura actual relevante:

- Layout procedural de nave (`ShipLayoutGeneratorTests`).
- Drenaje centralizado de `ShipHealth`.
- Eventos de estado de `RepairStation`.
- Consistencia entre `GameConfig` y Build Settings.
- Registro de jugadores en `LobbyPlayerSessionData`.

## Notas de estabilidad

- Evitar `SceneManager.LoadScene` fuera de `SceneLoader`.
- Evitar `FindObjectsOfType<RepairStation>()` en loops de runtime; usar `RepairStation.ActiveStations`.
- `PlayerInputHandler` usa suscripciones C# a acciones del New Input System. No mezclar con callbacks `SendMessages` en el mismo script.
- `Assets/Scripts/UI/ShipHealthUI.cs` queda solo como stub legacy auto-desactivado para no romper referencias viejas; la UI activa es `Assets/Scripts/UI/HUD/ShipHealthUI.cs`.
