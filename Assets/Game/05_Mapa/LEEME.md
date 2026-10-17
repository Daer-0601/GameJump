# recinto de mantenimiento

Escena de trabajo: `Assets/Game/00_Scenes/Demo.unity`.
Mapa activo: `Recinto - definitivo`.

## recorrido

Entrada y garita, depósito de carga con pasos interiores, patio oculto y dos destinos distintos: taller profundo y bombas. Los cambios de dirección evitan ver el destino desde la entrada. El piso continuo conserva la colisión entre sectores. Los techos cierran contra los muros; las puertas tienen dinteles y los objetos decorativos no ocupan el centro de las rutas.

Referencias: entrada al patio 128 m; patio al fondo del taller 70 m; patio al fondo de bombas 69 m. Entrada al destino: aproximadamente 62 s caminando o 35 s corriendo sin paradas. Esto no determina la duración de una partida completa; las tareas, errores, desajustes y coordinación requieren prueba humana de balance.

## arte e iluminación

Texturas SBS Horror 256x256 con repetición por metros, chapa oxidada, ladrillo de carga, revoque del taller y concreto húmedo. Bombas tiene suelo metálico. Se reutilizaron modelos con sus materiales del mapa de la rama `atmosfera-a-oscuras`: tuberías, válvula, generadores, barreras, pallets y barriles.

Luna azul tenue y cielo procedural estándar de Unity en exteriores. Techos opacos y sombras en faroles y vela para mantener los interiores oscuros. Vela cálida con alcance visual de 7 m; no se cambió el radio usado por las reglas de peligro ni la lectura de inscripción. Luces blancas de victoria redistribuidas por todo el recinto. Atlas adicional de sombras de 4096 para evitar que las sombras de la vela y los faroles se reduzcan demasiado.

## sonido

Se conservaron las alertas actuales de error, desajuste, peligro y derrota. La música de fondo se mezcla a volumen reducido. Viento localizado en entrada y patio; crujidos espaciados en taller y golpes de metal en carga y bombas. Los sonidos usan la distancia al jugador más cercano, paneo moderado y amortiguación por paredes; así se escuchan en el audio compartido de la pantalla dividida. No hay nuevos AudioListener.

Los pasos alternan cinco grabaciones por superficie, sin repetir consecutivamente la misma muestra. Metal en bombas y concreto en las demás rutas. Los componentes RecintoSound y RecintoFootsteps solo cambian la presentación sonora.

## conservar y trasladar

La copia del mapa anterior, con las funcionalidades nuevas, está en `Boceto/DemoAntesDelGraybox.unity`. Las demás escenas de Boceto son respaldos intermedios del montaje. No ejecutar los menús de construcción sobre ajustes manuales sin guardar antes una copia.

El prefab `RecintoDefinitivo.prefab` contiene el entorno y el audio ambiental. Para usarlo en otra escena hay que volver a conectar el campo `game` de sus componentes RecintoSound y los puntos de inicio y objetivos. La escena Demo actual ya contiene esas conexiones.

Los controles, menús, nombres, movimiento, carrera, inventario de una mano, reglas de manivela, lectura con vela, peligro, desajustes y estabilización se mantienen. No se modificaron sus valores de dificultad.

## verificación

Prueba automática en Play: controladores de ambos jugadores recorren las rutas completas, una sola vela y manivela inicial, inscripción ilegible sin vela y legible con ella, bancos de audio completos, error de orden con penalización, tres acoples y victoria. Revisión de cobertura de techos y capturas a altura del jugador. Falta una sesión humana entre dos para valorar miedo, mezcla en los altavoces reales y duración de la partida.
