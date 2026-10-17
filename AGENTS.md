# Instrucciones del proyecto para agentes

Antes de cambiar el juego, lee [`CONTEXT.md`](CONTEXT.md). Contiene la arquitectura y el alcance actuales; si el código o la escena contradicen el documento, inspecciona el proyecto y actualiza el contexto cuando corresponda.

## Skills del proyecto

- Para diseñar, evaluar o implementar interfaces y experiencia de juego, aplica [`video-game-ui-ux`](.cursor/skills/video-game-ui-ux/SKILL.md).
- Para scripts, escenas, sistemas, herramientas o depuración en Unity, aplica [`unity-expert`](.cursor/skills/unity-expert/SKILL.md).
- Si una tarea toca ambos temas, aplica ambos skills y resuelve primero los requisitos de experiencia antes de elegir la implementación técnica.

## Reglas de trabajo

- Conserva la compatibilidad con Unity **6000.5.6f1**, URP e Input System salvo que el usuario solicite una migración.
- Inspecciona el estado de Git y los archivos existentes antes de editar. Conserva cambios locales y no restaures ni reconstruyas escenas sin indicación explícita.
- Respeta los `.meta` de Unity y sus GUID. Añade el `.meta` correspondiente a cada recurso nuevo y deja que Unity genere referencias cuando sea posible.
- Mantén los cambios dentro del alcance pedido. Evita paquetes, sistemas o abstracciones nuevos si no aportan una necesidad concreta.
- Comunica qué archivos cambiaste y qué verificación realizaste. No afirmes que una escena o compilación fue probada si no se abrió o ejecutó en Unity.
