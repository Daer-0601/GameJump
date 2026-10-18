# A Oscuras — tareas para mejorar el juego

Fecha: 17/10/2026. Este reparto sustituye al anterior y describe cambios por implementar. Los pendientes técnicos del corte siguen registrados en `PRUEBAS_A_OSCURAS.md`.

## Andres — mecanismos con cooperación activa

Objetivo: que los dos jugadores tengan una acción durante la activación de un mecanismo.

1. Añadir un punto de apoyo en la carcasa existente: una persona mantiene E/U para sujetar el mecanismo mientras la otra acciona la manivela. Cualquiera puede asumir cualquiera de los dos papeles; sujetar debe permitir conservar la vela en la mano.
2. Hacer avanzar la activación solo mientras ambos colaboran. Si uno suelta o se aparta, detener el avance y mostrar qué falta. La ausencia del compañero no cuenta como error de orden ni suma peligro.
3. Conservar los requisitos de contraste, inspección y orden A/B/C. Mostrar respuestas distintas para «falta compañero», «datos sin contrastar» y «orden incorrecto».

Entrega jugable: activar un mecanismo exige coordinar dos acciones y permite intercambiar los papeles. La pausa interrumpe la interacción; al volver se puede retomar sin quedar bloqueados.

Zona de trabajo: `DemoMechanism.cs`, interacción en `DemoPlayer.cs` y reglas de activación en `DemoGame.cs`. Acordar con Julio la posición del punto de apoyo para que ambos jugadores quepan.

## Eduardo — amenaza más expresiva y terror ambiental

Objetivo: que el jugador perciba la amenaza y su evolución dentro del recinto.

1. Añadir variaciones sonoras para el aviso, desplazamiento y detención de la sombra, conectadas a sus estados reales. Mantener el aviso de 3 s y acompañar la información importante con texto en ambas mitades.
2. Incorporar dos momentos ambientales breves en salas existentes: un golpe detrás de una puerta y un parpadeo local. Cada uno ocurre como máximo una vez por expedición. El parpadeo ambiental no cambia inspecciones ni plazos de las luces X/Y.
3. Dar una respuesta audiovisual diferenciada al primer contacto y a la retirada de la sombra, respetando el control del jugador y el margen de recuperación existente. Reservar el susto de derrota para el final correspondiente.

Entrega jugable: se distinguen aviso, movimiento, detención y contacto; los eventos ambientales aportan tensión sin tapar las pistas necesarias para jugar.

Zona de trabajo: presentación de amenaza en `RecintoInvestigation.cs`, `RecintoSound.cs`, `DemoAtmosphere.cs` y `DemoPassage.cs`. Reutilizar los recursos de audio disponibles.

## Julio — personajes y escenario más creíbles

Objetivo: mejorar lo que se ve al moverse, interactuar y orientarse en la oscuridad.

1. Suavizar las transiciones entre caminar, correr, cargar y agacharse. Corregir la pose sostenida de interacción y ajustar las manos para que vela y manivela permanezcan sujetas durante esas transiciones.
2. Dar una referencia visual propia a TALLER 04 y BOMBAS 05 mediante sus carteles, materiales y elementos existentes. Hacer reconocibles sus accesos y los mecanismos al iluminarlos con la vela.
3. Añadir respuesta visual gradual a mecanismos: movimiento de rueda durante el uso y vibración localizada durante un desajuste. Mantener las señales ligadas al estado real y evitar flashes intensos.

Entrega jugable: personajes sin saltos de pose, objetos bien sujetos y salas finales identificables por el entorno. Coordinar la animación de la rueda con el progreso implementado por Andres.

Zona de trabajo: `DemoMotion.cs`, ajustes de agarre en `CarryObject.cs`, controladores y clips de `Assets/External/AOscurasProduction/`, materiales y edición incremental de `Demo.unity`.

## Edson — HUD y aprendizaje durante la partida

Objetivo: que cada jugador entienda qué hacer y por qué una acción se detuvo.

1. Reorganizar cada mitad del HUD en tres niveles: objetivo inmediato, interacción disponible y resumen compacto de marcas/recursos. Mantener los avisos de amenaza visibles sin cubrir el centro de la cámara.
2. Añadir ayudas breves la primera vez que se necesita soltar un objeto, contrastar registros o estabilizar un mecanismo. Usar las teclas propias de cada jugador; retirar cada ayuda cuando complete la acción y reiniciarlas al comenzar otra expedición.
3. Mejorar la contención con indicadores de ocupación de TALLER 04 y BOMBAS 05, una barra legible y el motivo concreto si retrocede: falta alguien en una sala o hay un mecanismo desajustado. Mostrar el estado real sin revelar datos aún no descubiertos.

Entrega jugable: ambos jugadores pueden seguir la secuencia sin explicación externa, con textos legibles a 1280×720 y 1920×1080. Los avisos indican una acción concreta para continuar.

Zona de trabajo: `DrawHud` y presentación de `DemoGame.cs`, textos contextuales de `RecintoStation.cs`. Coordinar con Andres el mensaje de cooperación; conservar IMGUI y los controles actuales.

## Coordinación de los cambios

- Andres define primero los estados y mensajes de la nueva cooperación; Edson conecta su presentación y Julio su animación.
- Andres edita reglas de `DemoGame.cs`; Edson edita su presentación. Acordar los bloques antes de integrar para evitar sobrescrituras.
- Julio coordina las ediciones de `Demo.unity`. Los demás le pasan los puntos y referencias que necesiten conectar.
- Cada entrega incluye el cambio funcionando y una comprobación breve en Play. El cierre técnico anterior permanece en `PRUEBAS_A_OSCURAS.md`.
- Leer `CONTEXT.md` y aplicar los skills del proyecto. Mantener Unity 6000.5.6f1, URP, Input System, `.meta` y GUID. Reutilizar la escena y los sistemas existentes; conservar el recinto y sus referencias.
