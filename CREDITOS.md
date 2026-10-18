# Assets de la demo

## recinto ampliado

- Texturas: Horror Texture Pack 256x256, Screaming Brain Studios, CC0. https://screamingbrainstudios.itch.io/horror-texture-pack — la página declara que no se utilizó IA generativa.
- Audio ambiental: 100 CC0 Metal and Wood SFX, rubberduck, CC0. https://opengameart.org/content/100-cc0-metal-and-wood-sfx
- Pasos y viento: Hazard Pay, gratuitos para uso comercial y no comercial, atribución opcional; no revender los archivos como pack. https://hazardpay.itch.io/40-free-psx-crunchy-footsteps y https://hazardpay.itch.io/free-psx-wind-weather-ambience — ambas páginas declaran que no se utilizó IA generativa.

Detalles del montaje y verificación: Assets/Game/05_Mapa/LEEME.md. Se reutilizan modelos y materiales del mapa previo de la rama; no se reemplazan los sistemas de juego. El montaje, UV y código ambiental tienen asistencia de IA. El cielo usa el shader procedural estándar de Unity.

Los recursos externos descritos aquí se descargaron gratuitamente de las páginas de sus autores. No se utilizaron muestras de pago. Las páginas de los tres recursos externos citados declaran «No generative AI was used». La geometría procedural propia se documenta por separado más abajo.

## Modelos y texturas

- **Creeping Shadow Creature**, de **Purple.Point**. Fuente: https://sketchfab.com/3d-models/creeping-shadow-creature-free-horror-3d-model-53e77576ca0b4c5988ba2991a4bff075 . Licencia declarada: CC BY. El modelo se convirtió de GLB a FBX, se redujo su malla y se extrajo su textura BaseColor original para el material URP; la obra original sigue perteneciendo a su autor.
- **Low Poly Dead Body Covered Game Ready**, de **-Arthur-**. Fuente: https://sketchfab.com/3d-models/low-poly-dead-body-covered-game-ready-1e6ae13f6c2e46429eaca4d22d8bdbaa . Licencia declarada: CC BY. Se usa el FBX original y su textura BaseColor como hallazgo ambiental.
- **LOVECRAFTIAN HORROR**, de **alexalbinyana**. Fuente: https://sketchfab.com/3d-models/lovecraftian-horror-6093cbb205444e9aa7b36d7be4ed9b58 . Licencia declarada: CC BY. Se usa el FBX original y las texturas difusas de cuerpo, encías y dientes como anomalía persistente.

- **3D Retro Shacks Shanties Sheds & Gantries**, de **chilly_durango**.
  Fuente: https://chilly-durango.itch.io/3dretroshacks
  Licencia declarada: CC0 1.0 Universal.
- **3D Retro Plumbing, Wiring & Machinery**, de **chilly_durango**.
  Fuente: https://chilly-durango.itch.io/3d-retro-plumbing-wiring
  Licencia declarada: CC0 1.0 Universal.

Adaptaciones: conversión de Blender a FBX, escala y materiales URP. Se conservan las texturas originales. Las tuberías y barricadas de los recorridos reutilizan estos mismos recursos.

- **Atrezzo del recinto** (barril, tornillo de banco, radiocasete, saco de cemento, manguera, caja de herramientas, cables, tuberías, neumático, taburete, silla, generador, carro de soldar, estantería, rata, carro de herramientas, bolsa de basura, señal de suelo mojado, escoba, caja, escalera y mesa de picnic), de **Poly Haven**.
  Fuente: https://polyhaven.com/
  Licencia declarada: CC0 1.0 Universal.
  Adaptaciones: exportación a FBX, materiales URP y colocación en el mapa. La estantería se escala para caber bajo los techos. La rata queda a tamaño real y se mueve con `RataSusto.cs`. Los registros, la consola y las luces reutilizan carro, generador, taburete, radio y caja de herramientas.

- **Binder Notebook 2k**, de **Poly Haven**.
  Fuente: https://polyhaven.com/a/binder_notebook
  Licencia declarada: CC0 1.0 Universal.
  Se usa como el cuaderno de procedimiento en mano (cuerpo, tapa y cierre) y como prop cerrado sobre los carros de registro.

- **Permanent Marker**, de **Font Bureau / Google Fonts**. Licencia Apache 2.0.
- **Kalam** y **Patrick Hand**, de **Google Fonts**. Licencia OFL (SIL Open Font License 1.1).
  Las tres se usan solo para el texto manuscrito del cuaderno, en `Assets/Game/07_Cuaderno/Resources/Cuaderno/Fuentes`.

## Modelos propios de A Oscuras

- **Trajes de mantenimiento, portavela, vela y manivela de baja poligonización.**
  Autoría: geometría original del proyecto creada proceduralmente con asistencia de Codex, sin modelos descargados ni rig humanoide.
  Fuente/URL local: [JamModels.cs](Assets/Game/03_Editor/JamModels.cs). No existe una URL externa para estas mallas propias.
  Archivos generados: `Assets/Game/02_Polish/*.asset` y materiales de esa misma carpeta.
  Licencia de estas mallas y materiales originales: **CC0 1.0 Universal**; se dedican al dominio público bajo https://creativecommons.org/publicdomain/zero/1.0/ . Esta dedicación se limita a la geometría y materiales nuevos, no modifica las licencias de otros recursos.
  Animación: balanceo y piernas mediante transformaciones de código en `DemoMotion.cs`; inserción visual de la manivela en `CarryObject.cs`.

El paso y segundo cartel de Daer usan geometría original simple generada en [DaerSetup.cs](Assets/Game/03_Editor/DaerSetup.cs), con asistencia de Codex, guardada en `Assets/Game/06_Daer/Meshes`. Esta geometría propia se dedica igualmente a CC0 1.0 Universal. Reutiliza los materiales existentes del taller; no añade texturas, modelos descargados ni audio externo.

El detalle adicional de trajes, visores, respiradores, guantes, cera y manivela es geometría original del proyecto, creada proceduralmente con asistencia de Codex en [JamDetails.cs](Assets/Game/03_Editor/JamDetails.cs). Fuente local: ese archivo y `JamModels.cs`; no existe URL externa. Mallas y materiales `Assets/Game/02_Polish/Rec_*`: **CC0 1.0 Universal**, https://creativecommons.org/publicdomain/zero/1.0/ . Las animaciones por transformaciones están en `DemoMotion.cs`, `DemoCandleFlame.cs` y `CarryObject.cs`.

## Audio

- **voice in the woods**, de **madmakingmistery**.
  Fuente: https://madmakingmistery.itch.io/voice-in-the-woods
  Licencia declarada: CC0 1.0 Universal.

Se conservan los clips existentes de `Assets/Game/04_Audio`: ambiente, pasos, luz, engranaje, alarma y susto. El loop de desajuste reutiliza un fragmento del engranaje y el aviso de error un fragmento de la alarma, generados en memoria; no se incorporó ni se atribuye una licencia nueva a esos archivos de audio. El chirrido de la rata reutiliza `wood_squeak_01.ogg` del pack de metal y madera de rubberduck ya citado. La pasada de presencias no descargó modelos ni audio nuevos.

Los sonidos localizados y bancos de pasos que ya incorporó el mapa nuevo conservan las fuentes y condiciones documentadas en [FUENTES.md](Assets/External/AudioFinal/FUENTES.md). Este pulido ajusta su mezcla y recuperación, sin descargar ni sustituir clips.

## Licencia CC0

https://creativecommons.org/publicdomain/zero/1.0/

Estos créditos documentan los recursos externos. El código y el montaje inicial de esta demo fueron realizados con asistencia de IA; no deben presentarse como trabajo hecho íntegramente sin IA.


## Demo de producción — 17 de octubre de 2026

- **Ultimate Animated Character Pack**, **Quaternius**, **CC0 1.0**. Fuente directa del autor: https://opengameart.org/content/animated-characters-pack . Se importan únicamente `Worker_Female.fbx` y `Worker_Male.fbx` en `Assets/External/QuaterniusWorkers`. Escala a 1,7 m, materiales URP/Lit más apagados y rig genérico. Se usan Idle, Walk, Walk_Carry, PickUp y RecieveHit originales. Las variantes de carrera y sostener objetos se adaptan en el Editor desde esos clips; no se utiliza Mixamo.
- **Universal Animation Library (Standard)**, **Quaternius**, **CC0 1.0**. https://quaternius.com/packs/universalanimationlibrary.html . Se incluye el FBX para Unity en `Assets/External/UniversalAnimationLibrary`. Los personajes finales usan sus clips de reposo, caminar, correr, agacharse, interacción, antorcha y golpe mediante retargeting Humanoid. La carpeta `Adapted` conserva una adaptación anterior al rig genérico de los operarios, que ya no es la presentación visible.
- **Personajes finales J1 male y J2 secretaria**. `male_casual.fbx`, `female_secretary.fbx` y sus texturas se reutilizan desde el proyecto local `D:\gamejamV2\Assets\Resources\Players`. Sus avatares se importan como Humanoid y usan la Universal Animation Library; los operarios anteriores quedan desactivados en la escena como respaldo.
- **Modular Dungeon**, **Quaternius**, **CC0 1.0**. https://opengameart.org/content/lowpoly-modular-dungeon . Únicamente `Candle.fbx` en `Assets/External/QuaterniusDungeon`; escala y materiales URP. La llama se sustituye por una pequeña malla de 8 cm creada en el Editor y animada según la carga; esta geometría procedural original (`Llama.asset`) se dedica a CC0 y no proviene de un generador de modelos.
- **Spider**, **br-n518**, **CC0 1.0**. https://opengameart.org/node/86785 . `Assets/External/RecintoSpider`: conversión del `.blend` original a FBX con Blender 3.6.23, preservando rig y clips WALK, IDLE y ATTACK, textura original de 256 × 256, escala y material URP/Lit húmedo y oscuro. WALK es el gateo de la sombra que puede hacer daño. IDLE es el reposo cuando una cámara la tiene a la vista. ATTACK es el amago, sin daño, del cruce breve al borde de la vista. No se descargó otro modelo para esa presencia: es la misma araña. Creeping Shadow y sus créditos se conservan como recurso anterior; no se fabrican animaciones para su esqueleto.
- **Fire Crackling**, **AntumDeluge**, **CC0 1.0**. https://opengameart.org/content/fire-crackling/ . Original `fire-1_0.ogg`, renombrado `Burn.ogg` en `Assets/External/AntumFire`; se reutiliza a volumen bajo para llama y consumo del brote.
- **Squish Sounds Effects**, **EZduzziteh**, **CC0 1.0**. https://opengameart.org/content/squish-sounds-effects . Original `squish_01_0.mp3`, renombrado `Crawl.mp3` en `Assets/External/EZSquish`; se mezcla a volumen bajo al gatear.
- **Plumbing** de **chilly_durango**, ya documentado arriba, **CC0**: se reutilizan `Circuit Breaker.fbx` para carcasas/paneles y `Pipe Valve.fbx` para la herramienta metálica. No se incorpora una licencia nueva. El golpe de compuerta reutiliza `metal_slam_01.ogg` de **rubberduck**, CC0, ya presente en AudioFinal.

Los controladores, clips adaptados y prefabs de `Assets/External/AOscurasProduction` se preparan con `DemoProduction.cs`. Sus materiales y adaptaciones se dedican a **CC0 1.0**, conservando la autoría de los originales. Cada carpeta nueva contiene `LICENSE.txt`. No se generaron modelos con IA; el código y el montaje tienen asistencia de Codex. No se añadieron paquetes ni recursos de pago. Licencia CC0: https://creativecommons.org/publicdomain/zero/1.0/ .
