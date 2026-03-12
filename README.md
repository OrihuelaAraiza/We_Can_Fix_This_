# We Can Fix This!

Juego cooperativo de reparación para 1–4 jugadores. Unity 2022.3 LTS + URP.

## Descripción

Los jugadores son la tripulación de una nave espacial averiada. Deben coordinar reparaciones, gestionar recursos y sobrevivir a fallos en cadena antes de que la salud del barco llegue a cero.

## Requisitos

- Unity 2022.3 LTS
- Universal Render Pipeline (URP)
- New Input System 1.14.2
- TextMeshPro

## Controles

### Teclado (hasta 4 jugadores)

| Acción | P1 | P2 | P3 | P4 |
|---|---|---|---|---|
| Moverse | WASD | ←↑↓→ | TFGH | IJKL |
| Saltar | Espacio | Enter | R | P |
| Interactuar | E | Numpad 0 | Y | O |
| Habilidad | Q | Numpad 2 | T | U |

### Gamepad

| Acción | Botón |
|---|---|
| Moverse | Stick izquierdo |
| Saltar | A / Cruz |
| Interactuar | X / Cuadrado |
| Habilidad | Y / Triángulo |

## Roles

| Rol | Perk | Penalización |
|---|---|---|
| **Fixie** | Repara más rápido | — |
| **Comandante** | Boost de velocidad al equipo | No puede reparar manualmente |
| **Hacker** | Reparación remota | No puede reparar Energía |
| **Mecánico** | Resetea degradación de estación | — |
| **Artillero** | Torreta desde cualquier lugar | — |
| **Saboteador** | Bomba eléctrica (desactiva drones) | — |

## Estructura del proyecto

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs         — estado global, aplica roles al iniciar
│   │   ├── LobbyManager.cs        — registro de jugadores y selección de roles
│   │   ├── PlayerData.cs          — ScriptableObject con stats de movimiento
│   │   ├── PlayerManager.cs       — spawn y setup de jugadores en gameplay
│   │   ├── PlayerRole.cs          — sistema de roles y habilidades
│   │   ├── RoleDefinition.cs      — ScriptableObject de definición de rol
│   │   ├── RoleSelectionData.cs   — persiste roles entre escenas (estático)
│   │   └── ShipHealth.cs          — salud de la nave, eventos estáticos
│   ├── Gameplay/
│   │   ├── CoopCamera.cs          — cámara dinámica coop con zoom automático
│   │   ├── PlayerInputHandler.cs  — bridge entre New Input System y componentes
│   │   ├── PlayerInteract.cs      — detección y hold de interacciones
│   │   ├── PlayerMovement.cs      — movimiento con Rigidbody
│   │   └── RepairStation.cs       — estación reparable (IInteractable)
│   └── UI/
│       ├── LobbyEventSystemFixer.cs   — auto-configura InputSystemUIInputModule
│       ├── LobbyPlayerJoiner.cs       — detecta dispositivos y registra jugadores
│       ├── LobbyUI.cs                 — UI del lobby con paneles por jugador
│       ├── PlayerLobbyInputHandler.cs — navegación de roles en lobby
│       ├── RepairProgressUI.cs        — barra de progreso de reparación
│       ├── RoleHUDElement.cs          — HUD de rol y cooldown de habilidad
│       └── ShipHealthUI.cs            — barra de salud de la nave
└── Editor/
    └── LobbyLayoutFixer.cs  — herramienta: WeCF → Fix Lobby Layout
```

## Escenas

| Índice | Nombre | Descripción |
|---|---|---|
| 0 | `01_Lobby` | Selección de roles, hasta 4 jugadores |
| 1 | `02_Gameplay` | Partida principal |

## Setup rápido

1. Abrir `01_Lobby` en el editor
2. Verificar que el GameObject `LobbyManager` tiene los componentes:
   - `LobbyManager`
   - `LobbyPlayerJoiner`
   - `LobbyEventSystemFixer`
3. Asignar los `RoleDefinition` ScriptableObjects en el Inspector de `LobbyManager`
4. El EventSystem de la escena debe tener `InputSystemUIInputModule`
   (o dejar que `LobbyEventSystemFixer` lo configure automáticamente)
5. En `02_Gameplay`, el prefab del jugador debe tener:
   - `PlayerMovement`
   - `PlayerInputHandler`
   - `PlayerInteract`
   - `PlayerRole`

## Notas técnicas

- Los eventos de `ShipHealth` son **estáticos** — suscribirse via nombre de clase, no `.Instance`
- `RoleSelectionData` persiste entre escenas como clase estática en memoria
- `PlayerManager.OnPlayerJoined()` aplica el rol guardado en el momento del spawn
- `GameManager.ApplySelectedRoles()` actúa como fallback con 0.5s de delay
