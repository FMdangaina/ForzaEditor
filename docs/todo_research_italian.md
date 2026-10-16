# TODO e ricerche (italiano)

Lista di idee e ricerche, raggruppata per argomento. Quando un punto parte, passa in `ROADMAP.md`
come voce di un topic (una sola voce alla volta sotto **Now**).

Stato: ⬜ da fare · 🔍 ricerca (prima capire, poi scrivere codice) · 🐞 bug segnalato

---

## A. Pulizia e base tecnica

- [ ] ⬜ **A1. Togliere Console Modding.** Elimina la dipendenza da `xdevkit.dll` (Xbox 360 SDK, non
      serve). File toccati: `ConsoleModding.cs` + `.Designer.cs`, `ConsoleFunctions.cs`,
      `ConsoleMethod.cs`, riferimenti in `ForzaStudioForm.cs` e `ArchiveEditorForm.cs`, riferimento
      in `ForzaEditor.csproj`. Lavoro piccolo, da fare prima di A2.
- [ ] 🔍 **A2. Portare a un .NET Framework recente.** Oggi il progetto è `v2.0` con XNA, x86.
      Da capire: fino a che versione arriva XNA senza cambiare il renderer (vedi B1), e cosa si
      rompe (SQLite, HexBox). Dipende da A1.
- [ ] ⬜ **A3. Finestra / pulsante Impostazioni** (percorsi giochi, Xenia, cartella DEV, opzioni viewer).
- [ ] ⬜ **A4. Tool di conversione in stile MDI** (finestre figlie dentro la finestra principale
      invece di finestre separate).

## B. Viewer 3D e materiali

- [ ] 🔍 **B1. Cambiare il renderer** (il "sogno"). Legato ad A2: XNA è il limite sia del framework
      sia della grafica. Prima decidere il sostituto, poi pianificare.
- [ ] 🔍 **B2. Materiali del gioco.** Capire se sono hardcoded nell'eseguibile o se esistono file
      shader da qualche parte (DVD, zip, `shadersettings`).
  - [ ] 🔍 B2.1 Se si trovano gli shader FM: usarli (o imitarli) nel viewer per vedere in anteprima,
        in tempo reale, l'aspetto in gioco.
  - [ ] ⬜ B2.2 Migliorare il carbin viewer con i materiali veri (dipende da B2).
- [ ] ⬜ **B3. Camera del viewer come quella del gioco:** FOV del gioco e proporzioni della viewport.

## C. Ruote e morph target

- [ ] 🔍 **C1. Morph delle ruote.** Da @Jew: *"it's dependant on the wheel having a morph target in
      its package"*. Sembra un flag che viaggia insieme allo shader e a `IsStock`; si parla di una
      texture: texture più grande = ruota ridimensionabile.
  - [ ] 🔍 C1.1 Confronto: il cerchio della BMW E30 non scala, quello della S60 sì. Sempre @Jew:
        *"You need morph targets in the wheel model for Forza to scale them to size"*.
        Nome da cercare nei file: `Morph_LOD0`.
  - Collegato alla larghezza gomme sulle auto portate (ROADMAP 1, "Tyre width").
- [ ] 🐞 **C2. Sostituzione manuale di una ruota da OBJ** (segnalato da @delete.diskord su Discord):
      *"i used forza studio to export an fm3 wheel to obj, then opened an fm4 wheel zip in
      forzaeditor and attempted to replace the wheel with the obj i exported, resulting in the
      'LOD 0 and LODs 1+ live in different files' error. however i did try the fm3 conversion (port
      car from another forza) in the new commit a little while ago and the wheel zip it output seems
      to work perfectly in fm4."*
      L'errore viene da `CarbinModel.cs` (l'OBJ ha LOD0 e LOD1+ insieme, ma nella ruota FM4 stanno
      in due carbin diversi). Fix: dividere l'OBJ tra i due file invece di fermarsi.
      Il port FM3 → FM4 invece funziona già.

## D. Altri modelli da studiare

- [ ] 🔍 **D1. Modelli traffico FH1/FH2:** aprirli e vedere com'è il formato.
- [ ] 🔍 **D2. Modelli FM2** (oggi FM2 è fuori scope, solo export; vedi ROADMAP "Later").

## E. Documentazione e community

- [ ] ⬜ **E1. Documentazione seria dei formati scoperti** (carbin, xds, Anark, gamedb, DLC...),
      in `docs/`.
- [ ] ⬜ **E2. Feedback da Discord.** Ancora pochi utilizzatori: raccogliere le segnalazioni
      (come C2) e metterle qui.

---

## Ordine proposto

1. Chiudere prima il **Now** della ROADMAP (9.3d: BMW X5 FM3 in gioco, texture troppo chiare e
   camera abitacolo).
2. **C2** – bug vero di un utente, piccolo e chiaro.
3. **A1** – piccolo, toglie una dll.
4. **C1** – ricerca morph ruote (serve anche ai port).
5. **A2 + B1** insieme – sono lo stesso problema (XNA); prima una ricerca, poi la decisione.
6. **B2 / B3**, poi **A3 / A4**.
7. **E1** man mano; **D1 / D2** quando c'è tempo.
