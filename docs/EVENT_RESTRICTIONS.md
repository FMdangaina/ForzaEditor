# EventRestrictions.Value

`EventRestrictions` (340 rows in FM4: `ID`, `EventID`, `BucketId` (always 0), `Value`) says which cars an event accepts. `Value` is a hex string of 5 to 15 bytes. All 340 values decode with no byte left over (`ForzaStudio\EventRestriction.cs`; Tools > Database SQL > Diagnostics > "Event restrictions decoded" lists them in words).

## Format

- **Flags, 5 bytes, big endian.** The first four bytes and then one byte called S: `flags = (b0 b1 b2 b3 << 8) | S`. Each set bit is one filter.
- **Values.** After the flags, the value of every set filter, in the fixed order below (1, 2 or 4 bytes, big endian).
- **All filters are required** (an AND). `0000000004` is "no restriction" (96 rows; the class tours and the challenges that name the car in the event itself).

| Order | Flag (before the shift) | Size | Filter | Id of |
|---|---|---|---|---|
| 1 | `0x01000000` | 1 | Class | `CarClasses.Id` (6 = S, 7 = R3). Two rows: the first career race (6) and the Pagani Zonda Cinque Roadster challenge (7). Meaning not fully sure |
| 2 | `0x00010000` | 4 | Year from | the year (`0x07C6` = 1990) |
| 3 | `0x00020000` | 4 | Year to | the year (`0x07B6` = 1974: "built before 1975") |
| 4 | `0x00200000` | 1 | Kind | `List_CarType.Id` (1 production, 2 tuned, 3 race) |
| 5 | `0x00080000` | 1 | Body family | `List_FamilyBody.Id` (2 city car ... 21 GT2: the "Open" events) |
| 6 | `0x00800000` | 1 | Region | `List_Region.Id` (1 Asia, 2 Europe, 3 America) |
| 7 | `0x00008000` | 2 | Car | `Data_Car.Id` (the Spec Series: 299 = Chevelle SS-454, 461 = Supra RZ) |
| 8 | `0x00002000` | 2 | Model family | `List_FamilyModel.Id` (the Club events: 24 = Miata / MX-5) |
| 9 | `0x00000800` | 2 | Make | `List_CarMake.ID` |
| 10 | `0x00000200` | 1 | Country | `List_Country.CountryID` |
| 11 | `0x00000080` | 1 | Cylinders | `List_Cylinders.CylinderID` (4 = 8 cylinders) |
| 12 | `0x00000008` | 1 | Engine layout | `List_EngineConfig.ConfigID` (4 = rotary) |
| 13 | `0x00000002` | 1 | Drivetrain | `List_DriveType.ID` (1 FWD, 2 RWD, 3 AWD) |
| 14 | S `0x10` | 1 | Doors | 2 (coupe) or 4 (sedan) |
| 15 | S `0x02` | 1 | Aspiration | `List_Aspiration.AspirationID` (1 = natural) |
| 16 | S `0x01` | 1 | not known (one row, "Boosted Proving Grounds": `07 01 01`) | |

S bit `0x04` is set on 338 of 340 values and has no byte (the two without it hold only the class filter). Not known what it marks.

## How it was found

By reading the event names and descriptions in the EN string table next to the values (Club events = one model family, "Classics before 1975" = year to 1974, the Spec Series = one car), then fitting the sizes (every value's length must equal 5 + the sizes of its filters) and the order (for every value with two or more filters, only the order that gives ids that exist in their tables). The order is certain for the pairs the data holds (year and kind and body before make, make before cylinders, engine layout and doors, kind and body and year before region, region before drivetrain); two filters never found together may be in the other order.

## Writing a restriction

A value for a new event: set the flag bit of every filter wanted, put S `0x04`, then the values in the order of the table. Example: Ferrari cars built before 1970 = flags `0002 0800 04`, year to `000007B1` (1969), make `000D`: `0002080004000007B1000D`.
