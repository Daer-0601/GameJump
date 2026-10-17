---
name: video-game-ui-ux
description: Diseña y evalúa UI/UX profesional para videojuegos, incluidos HUD, menús, flujos de interacción, accesibilidad y legibilidad en pantalla dividida. Úsalo al crear o revisar la experiencia del jugador o una interfaz del juego.
---

# UI/UX profesional para videojuegos

Diseña primero para la experiencia del jugador y luego para el sistema de UI. Alinea cada elemento con una decisión, una acción o una respuesta del juego.

## Proceso

1. **Entiende la tarea del jugador.** Identifica quién juega, qué sabe, qué necesita decidir y qué debe hacer a continuación. En cooperativo, revisa qué información es compartida y qué información pertenece a cada jugador.
2. **Traza el flujo.** Describe estados, entradas, transiciones, errores, pausa y salida. Incluye caminos de victoria, derrota, reconexión o falta de mando cuando existan en el alcance.
3. **Define jerarquía y feedback.** Ordena la atención por urgencia. Cada acción importante debe dar una respuesta visible, sonora o háptica coherente, y estados críticos deben tener más de una señal.
4. **Diseña para el espacio real.** En pantalla dividida, valida el HUD dentro de cada viewport, sus márgenes y la distancia de lectura. Evita colocar información compartida en el centro si interfiere con ambas vistas.
5. **Entrega una solución implementable.** Explica layout, estados, textos, comportamiento de foco/selección, entradas, escalado y assets necesarios. Si el usuario pide implementación, revisa primero la UI y convenciones existentes y edita el flujo real.
6. **Revisa el resultado en contexto.** Comprueba legibilidad, alineación, contraste, recortes, estados hover/seleccionado/deshabilitado, navegación de mando y teclado. Usa una captura o Play Mode cuando esté disponible; indica claramente qué no se pudo comprobar.

## Criterios de calidad

- Mantén una jerarquía visual clara, tipografía legible y espaciado consistente. La atmósfera puede ser oscura, pero el texto y las acciones deben seguir siendo fáciles de identificar.
- Usa etiquetas breves y verbos concretos. Muestra controles relevantes al contexto y adapta las pistas a teclado/mouse o mando.
- No dependas solo del color, del sonido, de partículas o de animación para comunicar estado. Añade texto, forma, icono o patrón cuando la información sea necesaria.
- Evita flashes intensos, movimiento innecesario y animaciones que bloqueen el control. Respeta opciones existentes de audio, movimiento, subtítulos y contraste; propón ajustes cuando aporten valor.
- Conserva consistencia entre HUD, menús, prompts y estados finales. No introduzcas una estética visual nueva sin integrarla con el arte y la iluminación del juego.
- Prefiere prototipos sencillos y probables con jugadores a adornos que no mejoren la comprensión o la coordinación.

## Aplicación a este juego

- Ayuda a la pareja a leer el orden de mecanismos, quién lleva la vela o manivela, qué interacción está disponible, el nivel de peligro y cómo pausar o reiniciar.
- Cada jugador debe poder leer su propia mitad de pantalla sin confundir el HUD de su compañero.
- La oscuridad debe sostener la tensión sin volver invisibles la inscripción, los mecanismos o las instrucciones de interacción.
- Las señales de luz y audio deben representar el estado real. No inventes tareas, estados de reparación o funciones que el prototipo no implementa.

## Entrega

Para una propuesta, incluye el objetivo del jugador, el flujo, jerarquía de pantalla, estados y criterios de accesibilidad. Para una revisión, prioriza los problemas según cuánto impiden entender o completar la tarea y explica dónde aparecen. Para una implementación, resume los archivos y estados editados y la verificación visual realizada.
