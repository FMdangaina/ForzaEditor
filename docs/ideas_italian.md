# Forza Editor — Roadmap tecnica e attività da completare

> **Obiettivo:** rendere più affidabili gli strumenti di editing e le procedure automatiche, verificare la compatibilità degli asset tra generazioni di Forza e standardizzare l'esportazione/importazione dei modelli.

## Priorità e ordine di lavoro

| ID | Attività | Priorità | Stato |
|---|---|---|---|
| 1 | Migliorare SQL Editor e mappatura dello schema DB | Alta | Da fare |
| 2 | Correggere la generazione automatica degli ID | Alta | Da fare |
| 3 | Aggiungere query di controllo prima degli `INSERT` | Alta | Da fare |
| 4 | Test di compilazione con traffic cars e asset di FM1/FM2 | Alta | Da fare |
| 5 | Individuare la posizione del pilota | Media | Da fare |
| 6 | Analizzare i suoni mancanti tra generazioni | Media | Da fare |
| 7 | Standardizzare export/import delle cartelle | Alta | Da fare |

---

## 1. Migliorare SQL Editor e comprendere lo schema del database

**Obiettivo:** rendere l'editor SQL più efficace e acquisire una conoscenza affidabile della struttura delle tabelle coinvolte.

### Attività
- [ ] Esaminare lo schema completo delle tabelle interessate.
- [ ] Identificare chiavi primarie, chiavi esterne, indici, vincoli e relazioni.
- [ ] Documentare il significato dei campi utilizzati dalle procedure di importazione, esportazione e compilazione.
- [ ] Verificare le dipendenze tra tabelle e l'ordine corretto delle operazioni.
- [ ] Migliorare l'SQL Editor per facilitare scrittura, esecuzione e verifica delle query.
- [ ] Preparare query diagnostiche riutilizzabili per consultare dati, relazioni e record collegati.

### Criteri di completamento
- [ ] Le tabelle coinvolte nel workflow sono documentate.
- [ ] È chiaro quali campi siano obbligatori e quali vincoli debbano essere rispettati.
- [ ] Le query di diagnosi possono essere eseguite senza modificare i dati.

---

## 2. Migliorare le procedure automatiche di gestione degli ID

**Obiettivo:** evitare assegnazioni arbitrarie o collisioni e rendere prevedibile la creazione di nuovi record.

### Regole operative
- La prima auto generata dal nuovo workflow deve partire dall'**ID 4000**.
- Gli ID successivi devono essere assegnati tramite una regola deterministica e verificabile.
- Non inserire ID scelti casualmente solo perché sembrano funzionare.
- L'ID `412` usato per la **RS6 Champion** è un esempio di soluzione funzionante, ma non conforme alla regola desiderata: non va preso come modello per le nuove auto.

### Attività
- [ ] Individuare tutti i punti in cui gli ID vengono generati, assegnati o copiati.
- [ ] Definire una singola regola di allocazione degli ID.
- [ ] Verificare che l'ID scelto non sia già presente nelle tabelle pertinenti.
- [ ] Gestire correttamente errori e tentativi di inserimento duplicato.
- [ ] Verificare se l'ID debba essere coerente tra più tabelle o riferimenti.
- [ ] Aggiungere log diagnostici che mostrino ID assegnato e motivo della scelta.

### Criteri di completamento
- [ ] La prima nuova auto usa un ID a partire da `4000`.
- [ ] Gli ID sono univoci secondo i vincoli effettivi del database.
- [ ] L'assegnazione non dipende da valori hardcoded sparsi nel codice.

---

## 3. Eseguire query di controllo prima degli `INSERT`

**Obiettivo:** validare i dati e i valori derivati prima di scrivere nel database.

### Attività
- [ ] Eseguire una query di controllo prima di ogni inserimento automatico.
- [ ] Verificare l'esistenza dell'ID e degli eventuali record collegati.
- [ ] Controllare che i campi obbligatori siano valorizzati.
- [ ] Calcolare e verificare il valore di `ordinal` prima dell'inserimento.
- [ ] Stabilire la regola corretta per `ordinal` sulla base dei dati reali e dei vincoli dello schema, senza assumere che sia semplicemente `MAX(ordinal) + 1`.
- [ ] Verificare possibili duplicati e riferimenti non validi.
- [ ] Quando possibile, eseguire controlli e inserimenti in una transazione, con rollback in caso di errore.
- [ ] Produrre un riepilogo diagnostico dei valori che verranno inseriti.

### Criteri di completamento
- [ ] Il valore di `ordinal` è calcolato secondo una regola documentata.
- [ ] Gli inserimenti non procedono se i controlli falliscono.
- [ ] Gli errori riportano la tabella e il campo coinvolti.

---

## 4. Test di compilazione con traffic cars e asset di FM1/FM2

**Obiettivo:** verificare se modelli provenienti da altre categorie o generazioni possono essere convertiti e compilati in modo funzionante.

### Piano di test
- [ ] Scegliere una o più **traffic cars** come primo caso di prova.
- [ ] In alternativa, selezionare un'auto o un asset proveniente da **Forza Motorsport 1/2**.
- [ ] Duplicare temporaneamente `LOD1` in `LOD0` per verificare se il processo di compilazione accetta una struttura semplificata.
- [ ] Compilare l'asset e verificare il risultato nel gioco o nel viewer disponibile.
- [ ] Registrare errori, warning e differenze rispetto a un asset nativo funzionante.
- [ ] Se la compilazione riesce, verificare geometria, materiali, UV, gerarchia e comportamento in gioco.
- [ ] Valutare, per gli ID compatibili, se sia possibile disattivare la **camera interna**.
- [ ] Separare i risultati per generazione del gioco e tipologia di asset.

### Matrice dei test

| Caso | Modifica | Verifica |
|---|---|---|
| Traffic car | Copia temporanea di `LOD1` in `LOD0` | Compilazione e caricamento |
| Asset FM1/FM2 | Adattamento minimo della struttura | Compatibilità del formato |
| ID con camera interna | Disattivazione della camera, se supportata | Caricamento e comportamento in gioco |

### Criteri di completamento
- [ ] Esiste almeno un test riproducibile con log e asset di partenza identificati.
- [ ] È documentato se la duplicazione di `LOD1` in `LOD0` sia sufficiente o quali dati manchino.
- [ ] L'eventuale disattivazione della camera interna è verificata, non soltanto ipotizzata.

---

## 5. Individuare la posizione del pilota

**Obiettivo:** trovare dove viene memorizzata la posizione del pilota e capire come viene interpretata durante il caricamento dell'auto.

### Attività
- [ ] Confrontare i file di una vettura funzionante con quelli di una vettura in cui la posizione è nota o corretta.
- [ ] Esaminare gerarchie, nodi, trasformazioni e riferimenti presenti nei file del modello.
- [ ] Verificare se la posizione dipenda da un nodo dedicato, da una matrice di trasformazione, da un riferimento alla camera interna o da dati esterni alla geometria.
- [ ] Controllare separatamente posizione, rotazione e sistema di coordinate.
- [ ] Effettuare una modifica controllata e verificare l'effetto nel viewer o nel gioco.
- [ ] Documentare file, struttura e campi responsabili, se identificati.

### Criteri di completamento
- [ ] La posizione del pilota è riconducibile a un dato o a una trasformazione specifica.
- [ ] La modifica è verificata con un test riproducibile.

---

## 6. Analizzare i suoni mancanti tra generazioni dei giochi

**Obiettivo:** individuare le cause dei suoni mancanti quando si trasferiscono auto o dati tra generazioni di Forza.

### Attività
- [ ] Confrontare una vettura con audio funzionante e una con audio mancante.
- [ ] Elencare i riferimenti audio presenti nei dati di entrambe.
- [ ] Verificare eventuali differenze di naming, ID, eventi, bank o dipendenze.
- [ ] Controllare se gli asset audio richiesti esistano nella generazione di destinazione.
- [ ] Distinguere tra riferimento mancante, asset assente, formato incompatibile e incompatibilità del sistema audio.
- [ ] Preparare una tabella di corrispondenza tra le generazioni coinvolte.
- [ ] Evitare di copiare riferimenti audio senza verificarne l'esistenza e la compatibilità.

### Criteri di completamento
- [ ] La causa è classificata per almeno un caso concreto.
- [ ] È documentata una procedura per individuare e correggere i riferimenti non validi.

---

## 7. Standardizzare l'esportazione e l'importazione delle cartelle

**Obiettivo:** rendere la struttura delle cartelle esportate coerente per **tutti i tipi di asset supportati**, così da semplificare modifica, confronto e reimportazione.

### Struttura desiderata

La cartella `car.carbin` deve contenere la struttura del modello, con `LOD0` e `body` come cartelle, includendo tutti gli oggetti pertinenti.

```text
<cartella_export>/
└── car.carbin/
    └── LOD0/
        └── body/
            ├── <oggetto_1>
            ├── <oggetto_2>
            └── ...