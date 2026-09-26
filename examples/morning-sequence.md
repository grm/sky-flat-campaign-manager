# Example — Morning sky flat sequence

1. Add **Sky Flat Campaign Container** after the last light frames
2. Mode: **Morning**
3. Wait for sky: **ON**
4. Pointing: **Alt/Az 70° / 270°**, Tracking **OFF** (defaults; use KeepCurrent if the sequence handles pointing)
5. Optional: place Ground Station notifications in:
   - **Campaign Required** — e.g. `🌅 {remaining} morning flats required`
   - **Campaign Not Required / Skip** — e.g. `✅ Sky flats skipped — campaign current`
   - **Campaign Completed**
   - **Session Incomplete**
6. For Ground Station on NINA 3.2, set the Ground Station message to `$$INSTRUCTION_SET$$`; SFCM resolves the event template into the parent event-container name.
7. Park / warm-up as usual after the SFCM container.

No outer Sun-altitude loop is required: SFCM handles the astronomical window and adaptive waiting itself.
