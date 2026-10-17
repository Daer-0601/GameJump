# Recuperación de miedo, sonido y presentación

Se comparó la escena actual con el pulido inicial (`08a746f`) y con la integración del mapa nuevo (`630d231`). Los recursos seguían presentes, pero el mapa nuevo había reducido el ambiente en LateUpdate a 0.035–0.075, reducido la niebla, activado el cielo y la luz lunar, y aumentado el alcance de la vela a 7 m. La nueva colocación de la manivela tampoco utilizaba la animación de inserción anterior.

## Resultado

- Oscuridad casi total fuera del área iluminada. Luna global a cero, cámaras con fondo oscuro, faroles de orientación a intensidad 0.08 y alcance 2.8 m. La vela conserva el color cálido, las sombras suaves, el parpadeo y un alcance de 5.8 m.
- La niebla sube de 0.03 a 0.06 con el peligro. Se mantienen viñeta, grano, aberración suave, luces blancas al ganar y apagado/fundido al perder.
- DemoGame vuelve a controlar el volumen del ambiente (0.16–0.42). Se conservan los sonidos locales del mapa, con amortiguación al atravesar paredes. Los pasos tienen más presencia y recurren al clip original si falta el banco de superficie.
- Los trajes existentes reciben visores, filtros de respirador, guantes, rodilleras, suelas y cintas. La vela recibe cera legible, gotas, mecha, asa y núcleo de llama animado. La manivela recibe empuñadura y acople con más detalle.
- Piernas, brazos y cuerpo acompañan la marcha, con más amplitud al correr. La mano y la vela reaccionan suavemente al giro. La cámara no recibe balanceo añadido.
- La manivela entra suavemente en el soporte. Pausar congela esa animación; retirarla durante el movimiento lo cancela sin devolverla al mecanismo. Una animación pendiente puede terminar durante la pantalla final.

La escena sigue siendo Demo.unity. El paso de Daer, los dos carteles, el orden aleatorio, los controles, las condiciones de victoria/derrota y los ocho valores de dificultad se conservan. El ajuste para cinco minutos sigue pendiente de las partidas con dos personas descritas en `Assets/Game/06_Daer/PRUEBA_DOS_PERSONAS.md`.

## Comprobar

Fuera de Play: **Gamejam > Pulido > Comprobar audio y presentación en Play**. Mide muestras de la salida de audio de Unity y de las fuentes; comprueba pasos, ambiente, avería, oscuridad, efectos URP, modelos e inserción. Resultado: `Temp/JamRecoveryCheck.txt`. La medición confirma señal en el motor; el volumen del dispositivo físico depende del equipo.

Después ejecutar **Gamejam > Daer > Comprobar en Play (sin registrar partidas humanas)** para los seis órdenes y el ciclo completo. Los dos comprobadores desactivan el registro de rondas humanas durante su ejecución.

Verificado el 17 de octubre de 2026: compilación del Editor y de scripts Player Windows64 correcta (27 ensamblados), **37 comprobaciones de presentación y audio** y **95 comprobaciones de Daer**, todas aprobadas. Se comprobó salida de audio real del motor, marcha/carrera, pose al sostener objetos, inserción cancelable y pausa, luces blancas al ganar y apagado/fundido al perder. La comparación de escena conserva todos los 1.346 objetos/componentes que existían antes de este pulido.

En una ronda normal, acercarse entre jugadores con la vela, caminar y correr para ver al compañero; colocar y retirar la manivela; alejarse de una avería y volver. Revisar que el cartel desaparece sin vela y que el sonido sigue presente al regresar. Ganar y perder en rondas distintas comprueba las luces blancas y el apagado final.

Geometría y materiales nuevos `Rec_*` son propios y CC0; autoría y fuentes en `CREDITOS.md`. El mapa se conserva y los detalles no añaden colisiones al jugador ni a los objetos. No se descargó audio nuevo.

Archivos principales: `DemoAtmosphere.cs`, `DemoMotion.cs`, `DemoCandleFlame.cs`, `CarryObject.cs`, `RecintoSound.cs`, `RecintoFootsteps.cs` y el fallback de pasos en `DemoGame.cs`; escena Demo y recursos `02_Polish/Rec_*`. Preparación y comprobación: `JamRecovery.cs`, `JamDetails.cs`, `JamRecoveryCheck.cs`, `JamModels.cs`, `JamPolish.cs`, `FinalRecinto.cs` y `DaerCheck.cs`. Las herramientas existentes del mapa ya no restauran los faroles brillantes ni asignan la inscripción inicial al segundo cartel.
