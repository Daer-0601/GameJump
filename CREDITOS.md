# Assets de la demo

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

## Audio

- **voice in the woods**, de **madmakingmistery**.
  Fuente: https://madmakingmistery.itch.io/voice-in-the-woods
  Licencia declarada: CC0 1.0 Universal.

Se conservan los clips existentes de `Assets/Game/04_Audio`: ambiente, pasos, luz, engranaje, alarma y susto. El loop de desajuste reutiliza un fragmento del engranaje y el aviso de error un fragmento de la alarma, generados en memoria; no se incorporó ni se atribuye una licencia nueva a esos archivos de audio.

## Licencia CC0

https://creativecommons.org/publicdomain/zero/1.0/

Estos créditos documentan los recursos externos. El código y el montaje inicial de esta demo fueron realizados con asistencia de IA; no deben presentarse como trabajo hecho íntegramente sin IA.
