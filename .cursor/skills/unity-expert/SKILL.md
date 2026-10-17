---
name: unity-expert
description: Implementa, depura y diseña sistemas de videojuegos en Unity con C#, escenas, componentes, Input System, URP y assets serializados. Úsalo al modificar código, escenas, importación o herramientas de Unity.
---

# Programación experta en Unity

Trabaja con el proyecto y la versión configurada. Para este repositorio, mantén compatibilidad con Unity 6000.5.6f1, URP e Input System, salvo que el usuario pida una migración.

## Antes de editar

1. Lee `AGENTS.md` y `CONTEXT.md`; inspecciona `ProjectSettings/ProjectVersion.txt`, paquetes y estado de Git.
2. Sigue las referencias reales de la escena y los componentes. Identifica qué objeto inicia y posee cada sistema, cómo se serializan sus dependencias y qué scripts ya resuelven el problema.
3. Revisa los cambios locales. No sobrescribas trabajo del usuario ni reconstruyas escenas. `Assets/Game/03_Editor/DemoBuilder.cs` puede reemplazar el montaje existente.
4. Elige la modificación más pequeña que encaje con la arquitectura actual. Añade una abstracción o dependencia solo si mejora una necesidad concreta.

## Implementación

- Usa componentes con responsabilidades enfocadas y APIs explícitas. Mantén el estado del juego como fuente de verdad y evita lógica duplicada en UI, audio y objetos.
- Usa `Awake`/`OnEnable` para preparar referencias y suscripciones, `Start` para dependencias ya inicializadas y `OnDisable`/`OnDestroy` para desuscribirte y limpiar recursos.
- Serializa referencias editables con `[SerializeField]` cuando el diseño requiera conexión desde Inspector. Valida dependencias críticas con errores que indiquen el objeto o campo faltante; no conviertas fallos reales en silencios.
- Evita búsquedas repetidas en `Update`, asignaciones innecesarias por frame, coroutines duplicadas y `FindObjectOfType` como localizador global. Cachea referencias; usa eventos para comunicar cambios cuando reduzcan acoplamiento.
- En Input System, usa el paquete y los bindings existentes. No mezcles APIs antiguas y nuevas sin una razón clara; conserva las entradas y el soporte local ya definidos.
- En audio y animación, separa disparo de eventos de presentación. Evita sonidos repetidos por frame y define volumen, espacialidad, loop y limpieza según el evento.
- En URP, usa componentes y materiales compatibles con el pipeline del proyecto. No agregues paquetes solo para una mejora cosmética menor.
- Para gameplay cooperativo, considera ambos jugadores, entradas simultáneas, pausa, final de partida y reinicio; protege las transiciones para que un evento no finalice el juego dos veces.

## Escenas y assets

- Respeta `.meta` y GUID existentes. Al mover assets, mueve también sus `.meta`; evita recrear referencias a mano si Unity Editor puede serializarlas.
- Prefiere editar escenas mediante Unity Editor. Si modificas YAML de escena directamente, conserva el formato, IDs locales y referencias, limita el diff y revisa la escena en Editor antes de darla por válida.
- No ejecutes `DemoBuilder` sin verificar el código y el efecto sobre la escena actual. No regeneres escenas por conveniencia.
- Mantén las escenas y prefabs fáciles de ajustar por el equipo: nombres legibles, componentes necesarios y valores serializados con intención.

## Verificación y reporte

- Elige una comprobación proporcional al cambio: compilar scripts, inspeccionar la escena, ejecutar Play Mode o usar `DemoCheck` en una sesión segura de Unity. `DemoCheck` puede cerrar el Editor; revisa su implementación antes de ejecutarlo.
- No afirmes compilación, prueba de Play Mode o verificación visual que no hayas realizado. Reporta la versión de Unity y cualquier limitación del entorno.
- Al terminar, resume el comportamiento, los archivos modificados, cómo reproducirlo y las comprobaciones realmente ejecutadas.
