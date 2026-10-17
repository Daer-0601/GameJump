# A Oscuras — pulido para Better Together

Escena: `Assets/Game/00_Scenes/Demo.unity`. Los controles efectivos siguen definidos en `DemoPlayerInput.cs`; no dependen del README antiguo.

## Prueba manual en Play

1. **Empezar sin mando.** Abrir la escena, Play, introducir nombres y pulsar Iniciar expedición. J1 debe llevar la única vela y J2 la manivela. Mover a ambos y probar Shift izquierdo/derecho. Escape pausa y permite continuar.
2. **Inscripción con y sin vela.** J2 se acerca a la inscripción central mientras J1 se queda atrás: debe ver «Acerca la vela para leer», sin orden. Al acercar la vela a menos de 3,8 m y pulsar U o E aparece el orden. Alejar la vela vuelve a ocultar ese mensaje. La vela soltada sigue funcionando; una pared entre vela e inscripción impide leer.
3. **Completar el orden.** Llevar la manivela por las rutas A/B/C según la inscripción. Probar un mecanismo incorrecto entre dos correctos: debe haber un sobresalto breve y aviso en las dos mitades, sin borrar el progreso. El acierto da alivio y conserva el giro del mecanismo.
4. **Estabilizar.** Después del primer acierto, esperar entre 38 y 52 s. Un mecanismo completado temblará, soltará chispas y traqueteará. Mirarlo a distancia de uso y mantener E (J1) o U (J2) unos 1,5 s: aparece una barra y el porcentaje. Soltar o apartar la mirada reinicia la reparación. No hace falta la manivela. El golpe final y la bajada de peligro confirman el arreglo. Pausar no debe dejar el loop sonando.
5. **Ganar y perder.** Con tres mecanismos hechos y alguno suelto, la partida continúa. Reparar el último desajuste permite ganar: se encienden luces blancas y aparece el tiempo. Reiniciar y separarse de la vela/ignorar desajustes permite llegar al 100 %: suena el susto de derrota, se apaga la llama y el escenario funde a negro. Los botones finales deben seguir visibles. La segunda vela permanece apagada en todos los casos.

## Peso de la manivela, estabilización y mirada de J2

1. **Peso para cualquiera de los jugadores.** Caminar con la manivela, soltarla con Q (J1) u O (J2) y repetir el trayecto. Con los valores por defecto, caminar pasa de 3,2 a 2,08 unidades/s mientras se lleva; correr pasa de 5,6 a 3,64. La vela no penaliza. Pasar la manivela al compañero debe transferir la penalización. `Crank Speed Multiplier` permite ajustar el valor (0,65 inicialmente).
2. **Soltar, colocar y retirar.** El HUD debe mostrar siempre `[Q/O] Soltar ... al suelo` mientras hay un objeto en la mano. Mirar un soporte con la manivela ofrece `[E/U] Colocar manivela en el soporte A/B/C`; después, con manos libres, ofrece retirarla. Colocarla recupera la velocidad y retirarla vuelve a penalizarla. Con la vela en la mano, el aviso pide soltarla antes de retirar. Pulsar soltar y usar simultáneamente debe dejar el objeto en el suelo, sin colocarlo o recogerlo de nuevo. Un orden incorrecto debe conservar su aviso de error.
3. **Inmovilidad durante la reparación.** En un mecanismo desajustado, mantener E/U y pulsar WASD/flechas con y sin el Shift del jugador: no debe caminar ni correr, ni generar nuevos pasos, desde el primer cuadro. La cámara sigue disponible y la gravedad continúa actuando. Soltar E/U, apartar la mirada o terminar la reparación permite moverse de nuevo; mantener E/U mirando un objeto que no está desajustado no debe bloquear el movimiento. El compañero sigue moviéndose normalmente. Pausar y reanudar no debe dejar un bloqueo residual. Probar tanto con la manivela en la mano como sin ella.
4. **Mirada de J2.** I/K mira arriba/abajo y J/L a izquierda/derecha. El giro acelera durante 0,12 s y se detiene al soltar cada tecla; cambiar de dirección no debe seguir girando en la anterior. I+L no debe girar más rápido que una sola dirección. Mantener Ctrl derecho reduce el giro al 35 % para apuntar a la manivela o la inscripción. Probarlo también tras pausar y reintentar; el mouse de J1 sigue independiente. En `DemoPlayer`, ajustar `Keyboard Look Speed`, `Keyboard Look Ramp Time` y `Precise Look Multiplier` si hace falta.

## Balance inicial

En «Partida - ajustar dificultad aqui»: `dangerPerSecond=0.31`, `darknessPenalty=0.6` por jugador sin luz, `errorPenalty=12`, `successRelief=10`, `unstablePenalty=1.25` por mecanismo, `stabilizeRelief=8`, `destabilizeMin=38`, `destabilizeMax=52`.

Sin aciertos ni reparaciones: ambos con luz pierden en unos 323 s; uno sin luz, unos 110 s; ambos sin luz, unos 66 s. Un desajuste agrega 1,25 puntos/s hasta repararlo. Tres aciertos recuperan 30 puntos; cada reparación recupera otros 8. Son estimaciones de las tasas, no tiempos medidos con jugadores: el objetivo de cinco minutos necesita una prueba humana completa, sin imponer una espera artificial para ganar.

## Comprobación del editor

`Gamejam > Comprobar pulido en Play` prueba controles, equipo inicial, lectura, progreso conservado, desajuste, interrupción de reparación, victoria condicionada, reinicio y derrota. Usa una partida temporal y vuelve al modo edición; no cierra Unity. Resultado detallado en `Temp/JamPlayCheck.txt`. `DemoCheck` se conserva.

Los menús `Gamejam > Pulido` aplican cada fase de forma incremental sobre la escena abierta; no reconstruyen el mapa con DemoBuilder. Los modelos simples se generan mediante `JamModels`, sin rig y con licencia documentada en `CREDITOS.md`.

## Archivos del cambio

Verificación final: compilación C# del jugador sin errores y comprobación en Play terminada con `ALL_PASSED`, incluidos los tres recorridos físicos. Capturas de los finales en `Temp/JamVictory.png` y `Temp/JamDefeat.png`. La duración y las sensaciones de una partida de cinco minutos todavía requieren probar con dos personas.

- Escena: `Assets/Game/00_Scenes/Demo.unity`.
- Reglas, señales e interacción: `DemoGame.cs`, `DemoPlayer.cs`, `DemoMechanism.cs`, `DemoClue.cs`, `CarryObject.cs` dentro de `Assets/Game/01_Scripts`.
- Componentes nuevos de presentación: `DemoAtmosphere.cs` y `DemoMotion.cs` en esa misma carpeta. No se añadió otro GameManager.
- Herramientas y verificación: `JamPolish.cs`, `JamModels.cs`, `JamPlayCheck.cs` en `Assets/Game/03_Editor`. `DemoBuilder.cs` y `DemoCheck.cs` solo recibieron protección `UNITY_EDITOR` para excluirlos del ejecutable.
- Mallas y materiales nuevos, con sus `.meta`: `Assets/Game/02_Polish`.
- Documentación: `CREDITOS.md` y esta guía. El esquema de `DemoPlayerInput.cs` y los clips de `Assets/Game/04_Audio` se conservan.
