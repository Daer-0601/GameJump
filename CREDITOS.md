# Assets de la demo

## recinto ampliado

- Texturas: Horror Texture Pack 256x256, Screaming Brain Studios, CC0. https://screamingbrainstudios.itch.io/horror-texture-pack — la página declara que no se utilizó IA generativa.
- Audio ambiental: 100 CC0 Metal and Wood SFX, rubberduck, CC0. https://opengameart.org/content/100-cc0-metal-and-wood-sfx
- Pasos y viento: Hazard Pay, gratuitos para uso comercial y no comercial, atribución opcional; no revender los archivos como pack. https://hazardpay.itch.io/40-free-psx-crunchy-footsteps y https://hazardpay.itch.io/free-psx-wind-weather-ambience — ambas páginas declaran que no se utilizó IA generativa.

Detalles del montaje y verificación: Assets/Game/05_Mapa/LEEME.md. Se reutilizan modelos y materiales del mapa previo de la rama; no se reemplazan los sistemas de juego. El montaje, UV y código ambiental tienen asistencia de IA. El cielo usa el shader procedural estándar de Unity.

Los recursos externos descritos aquí se descargaron gratuitamente de las páginas de sus autores. No se utilizaron muestras de pago. Las páginas de los tres recursos externos citados declaran «No generative AI was used». La geometría procedural propia se documenta por separado más abajo.

## Modelos y texturas

- **3D Retro Shacks Shanties Sheds & Gantries**, de **chilly_durango**.
  Fuente: https://chilly-durango.itch.io/3dretroshacks
  Licencia declarada: CC0 1.0 Universal.
- **3D Retro Plumbing, Wiring & Machinery**, de **chilly_durango**.
  Fuente: https://chilly-durango.itch.io/3d-retro-plumbing-wiring
  Licencia declarada: CC0 1.0 Universal.

Adaptaciones: conversión de Blender a FBX, escala y materiales URP. Se conservan las texturas originales. Las tuberías y barricadas de los recorridos reutilizan estos mismos recursos.

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

Se conservan los clips existentes de `Assets/Game/04_Audio`: ambiente, pasos, luz, engranaje, alarma y susto. El loop de desajuste reutiliza un fragmento del engranaje y el aviso de error un fragmento de la alarma, generados en memoria; no se incorporó ni se atribuye una licencia nueva a esos archivos de audio.

Los sonidos localizados y bancos de pasos que ya incorporó el mapa nuevo conservan las fuentes y condiciones documentadas en [FUENTES.md](Assets/External/AudioFinal/FUENTES.md). Este pulido ajusta su mezcla y recuperación, sin descargar ni sustituir clips.

## Licencia CC0

https://creativecommons.org/publicdomain/zero/1.0/

Estos créditos documentan los recursos externos. El código y el montaje inicial de esta demo fueron realizados con asistencia de IA; no deben presentarse como trabajo hecho íntegramente sin IA.
