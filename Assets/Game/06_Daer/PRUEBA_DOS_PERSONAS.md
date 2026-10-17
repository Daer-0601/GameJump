# Parte de Daer — A Oscuras

La escena lista para jugar es `Assets/Game/00_Scenes/Demo.unity`.

## Qué quedó implementado

- El primer mecanismo correcto abre una compuerta en el tabique del fondo del taller, en `(-14, 0, 90)`. Funciona si el primer símbolo es A, B o C. Los errores y las estabilizaciones no vuelven a disparar la apertura.
- Es un atajo adicional: el recorrido anterior por el este del taller sigue abierto desde el inicio. No se movieron mecanismos ni se alteró el orden aleatorio.
- El segundo cartel está detrás del paso, en `(-14, 1.5, 94.5)`, mirando hacia la entrada del taller. Con la vela encendida a menos de 3.8 m y sin obstáculos, muestra únicamente el siguiente símbolo pendiente. Se actualiza con cada acierto, conserva el símbolo tras errores y queda vacío al completar los tres. Al apartar la vela desaparece también la lectura del HUD.
- El objeto raíz es **Daer - paso y segunda pista**. `DemoPassage.onOpening` queda disponible para que Eduardo conecte el sonido de apertura, una sola vez. No se añadió audio nuevo.
- Cada victoria o derrota humana añade una fila al registro local `Playtests/daer-rounds.csv`, dentro de `Application.persistentDataPath`. En este proyecto Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\gamejam2026\Playtests\daer-rounds.csv`. Registra duración, resultado, tiempo hasta el primer acierto, máximo peligro, segundos con algún desajuste y mecanismos completados. No guarda nombres ni envía datos. Abandonar Play sin terminar no registra una ronda.

## Comprobación manual en Play

Usar los controles del código actual: J1 WASD / E / Q / Shift izquierdo; J2 flechas / U / O / Shift derecho. Mirada J1 mouse o F/H y T/G; J2 J/L e I/K. Esc pausa. Esta entrega no cambia teclas.

1. **Empezar sin mando.** Entrar en Play y elegir Iniciar expedición. Debe haber dos vistas, J1 con la única vela encendida y J2 con la manivela.
2. **Leer con y sin vela.** Acercar la vela a la inscripción inicial y usar E o U: da el orden completo. Apartarla más de 3.8 m: desaparece la lectura. Repetir con el cartel del taller: debe mostrar solo una letra, nunca toda la secuencia. Una pared entre la vela y el cartel también impide leerlo.
3. **Completar el orden y abrir el paso.** Colocar la manivela en un mecanismo equivocado: sube el peligro, se conserva el progreso y el paso sigue cerrado si aún no hubo aciertos. Retirar la manivela con la mano libre y colocarla en el primer símbolo correcto: la compuerta se recoge en 1.5 s y permite cruzar. Continuar por los símbolos pendientes. El segundo cartel cambia tras cada acierto; un error no lo adelanta.
4. **Estabilizar manteniendo.** Esperar un desajuste, mirar al mecanismo a distancia de uso y mantener E o U unos 1.5 s, sin exigir la manivela. Ver la barra en esa mitad de pantalla. Soltar antes de tiempo interrumpe el intento. El paso permanece abierto.
5. **Ganar y perder.** Completar los tres y reparar cualquier desajuste pendiente: victoria con tiempo final. En otra ronda, separarse de la vela, ignorar desajustes o cometer errores hasta 100%: derrota. Reiniciar: paso cerrado, nueva secuencia y reparto inicial de objetos restaurado.

## Prueba con dos personas pendiente

No se ajustó todavía la dificultad: faltan partidas humanas. El objetivo es que un equipo que ya entiende los controles pueda terminar normalmente en **4–6 minutos**, con opciones de remontar. El tiempo de la comprobación automática no sirve para balancear.

Hacer tres rondas sin cambiar valores entre ellas: una de aprendizaje y dos intentando coordinarse. Intercambiar quién ilumina y quién lleva la manivela. No usar teletransporte, comprobaciones automáticas ni cambios en el Inspector durante estas rondas. Anotar el tiempo de la pantalla final y contrastarlo con el CSV.

| Ronda | Duración | Ganaron | Primer acierto | Errores | Principal atasco / causa de derrota | ¿Se entendió el atajo y el cartel? |
| --- | --- | --- | --- | --- | --- | --- |
| Aprendizaje | | | | | | |
| Coordinada 1 | | | | | | |
| Coordinada 2, roles cambiados | | | | | | |

En **Partida - ajustar dificultad aqui** se conservan: `dangerPerSecond 0.31`, `darknessPenalty 0.6`, `errorPenalty 12`, `successRelief 10`, `unstablePenalty 1.25`, `stabilizeRelief 8`, `destabilizeMin 38`, `destabilizeMax 52`.

Después de observar las rondas, cambiar un factor cada vez y repetir dos rondas: si incluso coordinándose pierden antes de llegar al primer mecanismo, revisar recorrido y presión base; si estabilizar consume toda la partida, revisar intervalo de desajuste y su penalización; si los errores deciden todo, revisar su penalización. Si no se puede perder separándose o ignorando averías, aumentar la penalización correspondiente. No subir todo a la vez ni presentar estos criterios como un balance ya validado.

## Repetir la comprobación automática

Verificado el 17 de octubre de 2026 con Unity 6000.5.6f1: compilación del Editor correcta, compilación de scripts Player Windows64 correcta (27 ensamblados) y **95 comprobaciones en Play aprobadas**. Incluye entrada de teclado virtual U mantenida, progreso de estabilización por raycast, interrupción al soltar, luces blancas al ganar y apagado/fundido al perder. No es una medición de dificultad ni una partida humana.

Fuera de Play: **Gamejam > Daer > Comprobar en Play (sin registrar partidas humanas)**. Usa posiciones y órdenes controlados para comprobar los seis órdenes, colisiones del paso, lectura, pausa, reinicio, estabilización, victoria y derrota; sale de Play al acabar. Resultado en `Temp/DaerCheck.txt`. No añade filas al CSV humano. El montaje ya está guardado; no hace falta volver a ejecutar el preparador.

Archivos de esta parte: `DemoGame.cs` (eventos y datos de lectura), `DemoClue.cs`, `DemoPlayer.cs` (lectura del cartel), `DemoPassage.cs`, `DaerPlaytestRecorder.cs`, `DaerSetup.cs`, `DaerCheck.cs`, la escena Demo y las mallas de esta carpeta. Los tres constructores de mapa `FinalMapBuilder.cs`, `FinalRecinto.cs` y `FinalTexturePass.cs` quedaron protegidos con `UNITY_EDITOR` para excluir sus referencias a UnityEditor de la compilación Player.

La compuerta, el marco y el cartel usan geometría simple generada en el proyecto y reutilizan materiales existentes del taller. No se descargaron recursos nuevos. Para revertir solo el montaje, eliminar el objeto raíz de Daer y reactivar **Taller tabique oeste** dentro de **Recinto - definitivo**; el tabique original y los prefabs del mapa se conservan.
