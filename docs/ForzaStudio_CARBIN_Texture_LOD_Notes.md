# Forza Studio — CARBIN, texture e LOD

**Tipo:** nota privata di lavoro / TODO tecnico  
**Stato:** da verificare con asset reali; le ipotesi sotto non sono ancora confermate.

## Obiettivi

- [ ] Controllare il nome della texture associata ai modelli `.carbin` e verificare se Forza Studio può risolvere automaticamente il riferimento, evitando sovrapposizioni o assegnazioni errate di texture.
- [ ] Analizzare con attenzione LOD 0 e LOD 1, inclusi i riferimenti ai materiali e alle texture.
- [ ] Per i modelli che non hanno LOD 1, verificare se sia davvero necessario duplicare le texture di LOD 0 oppure se il caricamento possa gestire correttamente l'assenza del LOD.
- [ ] Verificare il caricamento delle texture da archivi esterni, non solo da file accanto al `.carbin`.
- [ ] Valutare anche il recupero/caricamento automatico dei soli rims (cerchioni), se utile al flusso di visualizzazione o sostituzione mesh.

## Ipotesi sulla struttura LOD da verificare

- **Forza Motorsport 1 e Forza Motorsport 2:** per auto e traffico, la struttura attesa da controllare è LOD 1–5, senza LOD 0.
- **Forza Motorsport 3 e successivi:** struttura attesa da controllare: LOD 0 più LOD 1–5.
- Non trattare queste regole come certe finché non sono state confrontate con modelli reali di ciascun gioco e con eventuali eccezioni.

## Texture condivise ed esterne

- [ ] Cercare texture richiamate da archivi esterni/condivisi.
- [ ] Controllare in particolare i file `.xds` presenti in `Media Cars Shared`.
- [ ] Verificare come risolvere i riferimenti tra `.carbin`, materiali, nomi delle texture e archivi condivisi senza duplicare inutilmente le risorse.
- [ ] Controllare se i rims possano essere individuati e richiamati separatamente.

## Strategia di verifica suggerita

1. Scegliere campioni reali di auto e traffico da FM1, FM2, FM3 e FM4.
2. Inventariare i `.carbin`, i nomi dei LOD, i riferimenti ai materiali e le texture presenti localmente e negli archivi condivisi.
3. Confrontare LOD 0 e LOD 1: nomi, materiali, riferimenti e dipendenze texture.
4. Provare un modello senza LOD 1 senza duplicare la texture di LOD 0; verificare se il renderer/editor gestisce correttamente il caso.
5. Implementare o correggere la risoluzione automatica dei riferimenti solo dopo aver identificato una regola affidabile, mantenendo una diagnostica chiara per riferimenti mancanti o ambigui.
6. Testare separatamente il caricamento dei rims.

## Nota terminologica

L'utente ha indicato gli `.xds` come file presenti in `Media Cars Shared` e ha dettato la sequenza “xilofono, Domodossola, Salerno”, interpretata come spelling fonetico di **XDS**.
