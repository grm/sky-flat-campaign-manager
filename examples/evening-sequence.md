# Example — Evening sky flat sequence

1. Connect equipment (camera, filter wheel; mount optional)
2. Cool camera as usual
3. Add **Sky Flat Campaign Container**
   - Mode: **Evening**
   - Strategy: **Adaptive**
   - Wait for sky: **ON**
   - Use SQM: no (or yes if weather device provides SkyQuality)
4. Optional: place Ground Station notifications in the blocking event containers:
   - **Campaign Required**
   - **Campaign Not Required / Skip**
   - **Before/After Wait**
   - **After Filter Complete**
   - **Campaign Completed**
   - **Session Incomplete**
5. For Ground Station on NINA 3.2, set its message to `$$INSTRUCTION_SET$$`; SFCM resolves the configured event template with live campaign values.
6. Continue with night imaging when the container returns.

An outer `Sky Flat Campaign Required` condition or twilight loop is optional: the SFCM container performs its own skip decision and window management.
