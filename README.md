# gamejam2026

Prototipo cooperativo local de terror para dos jugadores. Unity **6000.5.6f1**, plantilla **Universal 3D (URP)**, Input System. Este nombre es provisional.

## Abrir y jugar

1. Unity Hub → Add / Agregar proyecto desde disco → seleccionar esta carpeta.
2. Abrir con 6000.5.6f1. Esperar la importación de paquetes.
3. Abrir `Assets/Game/00_Scenes/Demo.unity` y pulsar Play.
4. En el menú, introducir dos nombres e iniciar la expedición. La partida actual usa un teclado compartido.

## Controles

- Jugador 1: WASD para moverse, mouse o F/H y T/G para mirar, E para usar o mantener, Q para soltar, Shift izquierdo para correr.
- Jugador 2: flechas para moverse, J/L e I/K para mirar, U para usar o mantener, O para soltar, Shift derecho para correr. Escape pausa.

Una sola mano: para agarrar la manivela hay que soltar la vela. Las velas siguen iluminando en el suelo. Cada partida reparte tres registros, una consola para ambos jugadores y dos puestos de luz entre ocho ubicaciones. Los registros revelan el orden; la consola y un número variable de luces completan la preparación. Después se activan los tres mecanismos con la manivela. La espera pasiva no termina la partida. Los errores apagan una luz o extienden la contaminación y aceleran apariciones. No hay campaña ni juego en red.

## Qué editar primero

- `Partida - ajustar dificultad aqui`: ritmo de peligro, penalización por error y alivio por acierto.
- `Jugador 1` / `Jugador 2`: velocidad, sensibilidad y alcance de interacción.
- Los objetos del escenario están guardados en la escena: moverlos desde el editor.
- `Assets/External`: modelos convertidos y texturas de los autores.
- `Assets/Game/Materials`: materiales de la escena.
- `Assets/Game/Scripts`: cinco scripts pequeños de partida, jugador, objetos, mecanismos e inscripción.

Los valores son iniciales de prototipo. Falta validarlos con jugadores y mandos físicos; la atmósfera no sustituye esa prueba. La escena usa cuerpos, velas y manivela provisionales de primitivas.

## Los cinco scripts del juego

- `DemoPlayer`: lee teclado/mouse o mando, mueve al jugador y detecta la interacción que está mirando.
- `CarryObject`: mantiene un único dueño por objeto y resuelve agarrar/soltar.
- `DemoMechanism`: exige la manivela y muestra si el mecanismo ya se completó.
- `DemoClue`: identifica la inscripción como objeto interactuable.
- `DemoGame`: orden de la secuencia, peligro, pausa, pantalla inicial, HUD y finales.

El montaje de la escena se hizo mediante `DemoBuilder`, un script del editor; el juego no construye el escenario por código al iniciar. Pueden editar la escena guardada directamente. `DemoCheck` es una comprobación del editor y no entra en el ejecutable.

## Herramientas del editor

- `Gamejam → Compilar demo Windows` genera `Builds/Windows/gamejam2026.exe`.
- `Gamejam → Crear escena de la demo` reconstruye la escena inicial. **Reemplaza el montaje actual de la escena**: guardar una copia antes de usarlo si ya la editaron.
- `DemoCheck.Run` verifica en modo Play las condiciones principales de la partida; se ejecuta en una sesión de Unity destinada a esa comprobación, porque cierra el editor al finalizar.

## Git

Repositorio local en rama `main`. No tiene remoto y no se publicó nada. Se guardan `Assets` con sus `.meta`, `Packages` y `ProjectSettings`. Se excluyen `Library`, `Temp`, builds, logs y configuración personal.

Para colaborar, creen o elijan un remoto y luego vincúlenlo. Todos deben usar la misma versión de Unity y evitar editar la misma escena simultáneamente.

Consultar `CREDITOS.md` para fuentes y licencias. La base de código y el montaje inicial tienen asistencia de IA; el equipo debe revisarlos y desarrollar su propia versión.
