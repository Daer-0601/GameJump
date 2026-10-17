# Contexto del proyecto

## Identidad y objetivo

`gamejam2026` es un prototipo de terror cooperativo local para dos jugadores. El nombre **A Oscuras** aparece en el concepto y sigue siendo provisional. La versión del editor del proyecto es **Unity 6000.5.6f1**, con **Universal Render Pipeline (URP)** y **Input System**.

## ¿De qué trata el juego?

Dos jugadores quedan atrapados en un recinto oscuro durante un incidente que deben neutralizar juntos. Solo cuentan con una vela y una manivela: tienen que iluminarse, repartirse las tareas y comunicarse mientras recorren el lugar. Una inscripción revela el orden correcto de tres mecanismos; activarlos en orden permite contener el incidente antes de que el peligro llegue al máximo.

La experiencia busca terror y tensión cooperativa, en tercera persona según el prototipo actual. El reto principal es coordinarse con información y recursos limitados: quién lleva la luz, quién transporta la manivela, cuándo dejar la vela y cómo evitar errores mientras el peligro aumenta. La oscuridad, la iluminación y el sonido acompañan la presión, pero deben dejar claro qué ocurre y qué acción se espera.

El prototipo se juega en una sola escena, `Demo.unity`, que también es la única entrada de Build Settings. El menú no tiene escena propia: `DemoGame` lo dibuja sobre la partida antes de empezar, con el fondo `Assets/Game/02_Materials/UI_menu/Menu.jpg`. Incluye el título **A Oscuras**, el nombre de cada jugador, **INICIAR EXPEDICIÓN**, **OPCIONES** y **SALIR**. Los nombres y el volumen quedan en `PlayerPrefs`. Iniciar no carga otra escena. La expedición actual se juega con un teclado compartido y no exige mando.

Pausa, victoria y derrota usan esa misma interfaz. Desde la pausa también se abre **OPCIONES**. **Volver a intentar** recarga Demo y continúa con los nombres guardados. **Volver al menú** recarga Demo y se detiene en el menú. El volumen se conserva entre sesiones.

## Flujo y reglas del juego

1. Cada expedición distribuye aleatoriamente seis puestos entre ocho ubicaciones del recinto: tres registros, una consola de contraste para ambos jugadores y dos luces auxiliares. Entre cero y dos luces son necesarias según la partida. Los registros descubren el orden de los mecanismos; la inscripción original se bloquea hasta completar la investigación.
2. Cada jugador tiene una mano. Para llevar la manivela, debe soltar la vela; las velas continúan iluminando cuando están en el suelo.
3. Los jugadores encuentran y activan los mecanismos A, B y C en el orden indicado.
4. La espera pasiva y la oscuridad ya no aumentan el peligro. Los errores de procedimiento y los mecanismos desajustados sí lo hacen. Un error también apaga una luz auxiliar activa o extiende la contaminación orgánica.
5. Completar la secuencia gana la partida. Llegar al 100% de peligro la termina en derrota. El prototipo permite reiniciar.

El diseño previsto contempla pantalla dividida local, una vela móvil, la manivela como objeto compartido y señales de audio y luz para comunicar estados. No hay multijugador en red, ni una escena de menú aparte, ni reparación de bombillas, ni campaña.

## Estructura actual

- `Assets/Game/00_Scenes/Demo.unity`: escena principal del prototipo.
- `Assets/Game/01_Scripts/`: scripts de partida, jugador, objetos, mecanismos, inscripción e investigación. `RecintoInvestigation` distribuye los puestos al iniciar, `RecintoStation` resuelve sus interacciones y `RecintoWorm` anima decoración pequeña. Menú, pausa, finales y HUD viven en `DemoGame` con IMGUI; no hay otra escena de interfaz.
- `Assets/Game/02_Materials/`: materiales del escenario.
- `Assets/Game/03_Editor/`: `DemoBuilder` y `DemoCheck`, herramientas del editor. El builder puede reemplazar el montaje de la escena; inspecciona su comportamiento antes de ejecutarlo.
- `Assets/Game/04_Audio/`: ambiente y efectos de sonido.
- `Packages/manifest.json`: dependencias de Unity.
- `ProjectSettings/ProjectVersion.txt`: versión del editor fijada por el proyecto.

El flujo principal vive hoy en una escena. Inspecciona `DemoGame`, `DemoPlayer`, `CarryObject`, `DemoMechanism` y `DemoClue` antes de diseñar una solución; no des por hecho que el README, el concepto o una conversación anterior describen exactamente el código actual.

## Controles del prototipo

- Menú y opciones: flechas, stick, cruceta o ratón para elegir; Enter o A confirman. En un nombre, el teclado escribe. OPCIONES guarda volumen general, ambiente, efectos, pantalla completa y resolución. Esc o B regresan.
- Partida, teclado compartido: jugador 1 usa WASD, mouse o F/H y T/G para mirar, E para usar, Q para soltar y Shift izquierdo para correr. Jugador 2 usa las flechas, J/L e I/K para mirar, U para usar, O para soltar y Shift derecho para correr. Escape pausa.
- El mando navega el menú. La partida actual no lo usa para moverse ni interactuar.

Comprueba los controles en código antes de cambiarlos y mantén señales claras de qué jugador puede realizar cada acción.

## Dirección de experiencia

- Usa oscuridad y tensión sin ocultar información necesaria para jugar.
- La interfaz debe ayudar a coordinarse: nombres, orden, progreso, peligro, objeto que lleva cada jugador, interacción disponible, pausa y resultado.
- En pantalla dividida, diseña para que cada mitad conserve jerarquía, legibilidad y espacio útil.
- El sonido y la iluminación deben reforzar estados reales del juego y tener niveles de volumen razonables. El ambiente tiene dos loops: `AmbientBackground` a volumen fijo y `Ambiente - audio original CC0`, cuyo volumen sube de 0.16 a 0.42 con el peligro. El control de ambiente los escala a los dos. La alarma, el desajuste, el engranaje, la apertura del paso y el susto final son efectos aparte.
- Los puestos, brotes orgánicos y cuatro piezas de utilería reutilizadas del mapa se generan al iniciar la partida. Dos cuerpos cubiertos importados aparecen en ubicaciones del recorrido: uno junto al depósito y otro en una de tres zonas variables. Conservan la rotación de importación para quedar acostados. Al acercarse a un cuerpo suena una señal breve; el primero puede cambiar de sitio después de que lo descubran, solo cuando ambos jugadores estén lejos y no miren ni el origen ni el destino. Los rótulos alterados por error tampoco se restauran mientras alguno pueda verlos. La aparición usa una malla importada de Creeping Shadow con su rotación y textura BaseColor originales y puede cruzar una abertura cercana del depósito o del taller cuando el hueco entra en el campo visual de alguno; el primer encuentro elegible ocurre y el segundo varía por partida. El modelo original tiene esqueleto de 34 huesos pero no trae clips de animación; la conversión actual a FBX conserva solo la malla, y el cruce por traslación necesita rediseño. Un ente Lovecraftian Horror aparece desde el inicio en uno de dos cuartos finales, da una señal al acercarse y permanece durante toda la partida. Falta validar en Play Mode ubicación, visibilidad y balance con dos personas.
- Los carteles generales del recinto muestran códigos de sector. Un error de procedimiento altera temporalmente uno de esos rótulos; las marcas A/B/C de los mecanismos no se falsifican.
- Mantén accesibles textos y señales importantes. No comuniques peligro, progreso o interacción solo mediante color o solo mediante audio.

## Assets y cambios

- Conserva nombres, GUID y archivos `.meta` existentes.
- Los assets importados pueden tener licencias o créditos: revisa `CREDITOS.md` antes de reemplazarlos o redistribuirlos.
- Evita regenerar o reubicar contenido en masa. Una reorganización de carpetas en Unity debe mover el asset junto con su `.meta`.
- Abre `Demo.unity` en la versión fijada antes de hacer cambios editoriales sobre la escena; no uses `DemoBuilder` para regenerarla sin autorización porque reemplaza el montaje existente.
