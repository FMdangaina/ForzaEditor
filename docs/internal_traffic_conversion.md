Creato pack auto traffic. - per controllare va fatto punto che prende dai dlc e visualizza albero. gestire importazione da dlc diretta.
    -Mesh ok, camere ok,  per ora yukon il mesh è abbastanza ok, da gestire alcune cose come targhe, collisioni e maledetti materiali.

    -per discorso materiali, ho visto che i mesh sono raggruppati male. dare la possibilità dall'albero nodi del carbin di fare una creazione dei nodi e lo spostamento dei vari mesh. così da riorganizzare (a mano) le cose. su traffic car sarà necessario al 100%.
    
    -Collisioni da controllare (yukon last), controllare che le collisioni non siano sotto il modello, sembra che tocchi la parte sotto. - test ingame, collisioni sbordano. fare editor collisioni con anteprima delle collisioni.
    Miglioramento di questa funzionalità necessario su consiglio di @blackbirdosaka
    "Slight problem, i need to rename the glass LTL and RTLs detail_glass_amber so that i have one that stays as it is, the other becomes lights_glass. But every time i change one, it changes both instead "
    -rivedere, sulla base di quello che mi è stato detto ed elaborare il funzionamento.
    
    -porting suoni non confermato, donor sound ok. può andare bene.
    
    -problema RAV4 ed XC70 (showroom non si vedono cerchi - in gara solo        (lod0?) ), problema ruote lod0 manca il nodo, bastarebbe generare il nodo dalla ruota lod0? verificare differenze tra rav4, xc70 e yukon.

    -opzioni tuning assenti, prendere base delle opzioni tuning e fare un generico per i traffic car (configurabili tramite .ini). sospensioni livello 0 1 2 3 sempre più rigide e con altezza diversa, barre anti rollio sempre più rigide tra i livelli, motore fai una stima della media tra livello 0-1-2-3. calcola dimensione cerchio originale e gestisci in base a dimensione gomma alcune possibità di editing, larghezza gomme da stock a +30% in step di misure di gomme vere. stesso discorso per i cambi, non toccherei i rapporti originali, farei solo frizioni che migliorano tempi, cambio che migliora i tempi.

ps. portare tutte le settings su INI chiari e precisi.    

Progetto materiali WIP (2026-10-19), su un progetto a parte:
    -decodifica semplice e di base degli shader del gioco e resa simile a quella in gioco, per avere un'anteprima fedele.
    -sostituisce ROADMAP 3k (materiali luci posteriori nelle parti anteriori) e fa rivedere 4.1 (somiglianze tra shader).
    -interni troppo scuri delle traffic car: non sistemato, forse dipende da questo.
    -collegato a ROADMAP 14.1e.2 (modello già texturizzato con i suoi .xds) e 13.2.




    