# A Oscuras — demo de producción

Unity 6000.5.6f1, URP 17.5, Input System. Única escena: `Assets/Game/00_Scenes/Demo.unity`. No ejecutar los builders antiguos. Esta guía sustituye las tasas y finales del pulido anterior.

## Teclado compartido

- J1: WASD; mouse o F/H y T/G para mirar; E usar/mantener; Q soltar; Shift izquierdo correr.
- J2: flechas; J/L e I/K para mirar; U usar/mantener; O soltar; Shift derecho correr; Ctrl derecho precisión al 35%.
- Esc pausa. El mando navega menús. Una mano por persona: J1 empieza con la única vela; J2, con la manivela.

## Recorrido manual

1. Play e iniciar desde el menú. Comprobar ambos cuerpos completos al caminar, la mano con objetos y la cámara al pasar cerca de una pared. Animar reposo, caminar, correr, carga lenta, vela, agacharse, mantener interacción y respingo. Los dos operarios deben ser distintos.
2. Leer el tablero de cada mitad: tres marcas, consola pendiente/verificada, inspección de red X/Y, plazos X/Y, objeto, tarea siguiente y peligro en palabras más porcentaje. Revisar a 1280×720 y 1920×1080, también tras pausa/opciones. No depende solo del color.
3. Explorar los seis puestos variables. Hay tres registros entre ocho posibles ubicaciones. Dos se leen manteniendo 2 s y uno pulsando tres veces en menos de 4 s. Exactamente una marca es falsa, tanto en la placa como en el aviso y el tablero. La consola necesita los tres datos y E/U simultáneos durante 2,5 s. Uno solo no debe completarla. La corrección identifica registro, marca falsa y marca verdadera. Usar un orden incorrecto suma 12%; cualquier intento sin contraste también cuenta como procedimiento incorrecto, aunque una marca coincida por casualidad. El soporte avisa antes de colocarlo.
4. Con la vela en la mano, apuntar al brote que tapa un panel y mantener E/U 2 s. Debe reducirse, consumirse, habilitar el panel y recuperar 30 s de cera. Soltar la tecla o mirar a otro sitio interrumpe la limpieza. La vela tarda 360 s activos en bajar de 7 m a 2,8 m; chisporrotea por debajo del 50%. El mínimo y estar a oscuras no suman peligro. Soltarla conserva la luz. Pausa congela el consumo.
5. Activar las luces de red necesarias (0–2 según expedición), manteniendo 2,5 s. X/Y anuncian un plazo aleatorio de 45–70 s. Apagar con una pulsación antes de vencer cancela la trampa y conserva la inspección. Dejar vencer: parpadeo, metal, subtítulo en ambas mitades y otro tramo posible de sombra.
6. Abandonar la vela lejos de ambos durante 7 s, o cometer un error: aparece «Algo cruza fuera de la vela», con 3 s de margen. La sombra debe gatear a 1,1 m/s por tramos de hasta 7 m; mirar desde cualquiera de las cámaras la detiene si no hay una pared. Debe seguir la vela, incluso cuando otro jugador esté más cerca en la dirección opuesta. Al llegar a un puesto lo contamina y lo anuncia. Primer contacto a menos de 1,6 m fuera de luz/cámara: +15%, respingo corto y retirada de unos 9 m; después hay 15 s de recuperación. Otro contacto termina por sombra. El aviso no debe borrarse al interactuar con un panel.
7. Descubrir los cuerpos: el primer hallazgo puede mentir mientras no haya contraste. Los siguientes no dan otra letra. Solo se recolocan con ambos lejos y sin mirar origen/destino. En la sala del ente, «No os separéis aquí» aparece una sola vez, con audio bajo. No debe sonar el susto de derrota durante estas exploraciones.
8. Llevar la manivela (velocidad al 65%) a A/B/C en el orden verificado. Comprobar colocación, giro y recuperación. Un acierto alivia 10%; el primero abre la compuerta durante 1,5 s con un solo golpe y frase. Cada 38–52 s tras el primer acierto puede desajustarse un mecanismo. Uno suma 1,25%/s; dos, 3,25%/s. Mantener 1,5 s, quieto y sin manivela, estabiliza y resta 8%. Soltar o apartar la mirada reinicia el trabajo. Esc debe cortar loop y chispas.
9. Tres aciertos estables inician la contención. Una persona ocupa TALLER 04 (centro -20,105) y la otra BOMBAS 05 (34,109); Toda la sala cuenta, también a más de 3 m del centro señalado. Mantenerse 25 s. Si salen o hay desajustes, retrocede a 1,7×. La vela falla 0,28 s cada 4,7 s. Al completar se encienden las luces blancas existentes y solo se emite un final.
10. Probar derrota por segundo contacto, por errores y por desajustes hasta 100%. El final debe mostrar la causa real. Pausa, reintentar y volver al menú deben restaurar combustible, contactos, puestos, marcas, orden, progreso, trampas y peligro; nombres y volumen se conservan.

## Verificación ejecutada

`Gamejam > Producción > Comprobar demo en Play` ejecuta `DemoProductionCheck` sobre la escena guardada y sale de Play al terminar. No reconstruye el recinto. Es una prueba programática con entradas sintéticas, posiciones controladas y tiempo acelerado durante la contención; no simula una expedición humana completa. Resultado y capturas en `Temp/Production.check` y `Temp/Production*.png`. Compilación adicional de C# con las referencias del proyecto: sin errores. Hay avisos obsoletos de las herramientas antiguas de Unity.

Las ejecuciones anteriores de regresión terminaron con `ALL_PASSED`: dos rigs, blends de caminar/correr conducidos por velocidad física, cámara y vela vertical, un listener, controles compartidos y precisión, vela en suelo y al mínimo sin daño, pausa, brote bloqueado/quemado/recarga, registro falso y contraste simultáneo (un jugador no basta), temporizador de luces/cancelación, sombra detenida en cámara, gateo con ruta al dejar de mirarla, primer/segundo contacto, tres sellos sin victoria inmediata, dos desajustes acelerados, reparación sin manivela, loop en pausa, contención/retroceso, victoria única, luces blancas y reinicios. Las ocho ubicaciones posibles pasan la comprobación de cápsula de acceso y línea de visión al panel; una ubicación del taller se desplazó 1,5 m para separarla del tabique. Auditoría de la escena: conserva los 1.579 objetos/componentes originales, con cero IDs eliminados y una única referencia a DemoGame.

Al corte del 17/10/2026, el último `ALL_PASSED` registrado es anterior a los ajustes finales. Falta repetir Play para verificar específicamente que la sombra elija la vela aunque otro jugador esté más cerca, que la contención cuente toda la sala y que la pose de interacción agachada conserve posiciones y escalas. También deben revisarse el dato aleatorio del primer cuerpo, el aviso de poca luz y la conservación de `runInBackground` al salir de la prueba. El archivo de ajustes actualmente conserva `runInBackground: 0`.

Existe un build Windows en `Build/AOscuras/AOscuras.exe`; `Temp/Production.build` registra `Succeeded bytes=146998685 errors=0`. Ese build precede a los últimos ajustes: falta recompilar después de las pruebas, abrir el ejecutable y preparar controles, créditos y licencias para distribuirlo. El reparto y los criterios de entrega están en `TAREAS_EQUIPO.md`.

Falta una partida humana completa de 12–18 minutos para medir orientación, mezcla de audio, frecuencia de errores, tensión y duración. Ese intervalo es objetivo de diseño, no una duración acreditada por la prueba automática.

## Archivos y packs

- Escena editada incrementalmente: `Assets/Game/00_Scenes/Demo.unity`.
- Reglas/HUD: `DemoGame.cs` y `DemoClue.cs`; objetos: `CarryObject.cs`; interacción/cámara: `DemoPlayer.cs`; montaje/estabilización: `DemoMechanism.cs`; presentación: `DemoMotion.cs`, `DemoCandleFlame.cs`, `DemoAtmosphere.cs`, `DemoPassage.cs`, `RecintoSound.cs`.
- Investigación y puestos: `RecintoInvestigation.cs`, `RecintoStation.cs`; nuevos `RecintoContamination.cs` y `RecintoShadowPath.cs` (cuadrícula de colisiones sin paquete añadido).
- Editor: nuevos `DemoProduction.cs` y `DemoProductionCheck.cs`. `DemoProduction` aplica modelos, materiales, controladores y clips sin regenerar el mapa.
- Packs CC0: `Assets/External/QuaterniusWorkers` (dos FBX), `QuaterniusDungeon` (vela FBX), `RecintoSpider` (FBX con rig y clips + textura), `AntumFire` y `EZSquish` (audio), `AOscurasProduction` (materiales URP, controladores, clips adaptados y prefabs). Cada carpeta incluye licencia. Carcasas y válvula reutilizan Plumbing de chilly_durango; golpe, AudioFinal de rubberduck. Autor, URL y adaptaciones completas en `CREDITOS.md`.
- Contexto y créditos actualizados. README y paquetes no se modifican. `UpgradeLog.htm` ya estaba sin seguimiento al empezar y se conserva.
