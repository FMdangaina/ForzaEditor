# FM4 game database (`gamedb.slt`): schema, relations, order of operations

Generated from the FM4 DVD1 database and `GameDbSchema` (`tools/DbRulesTest/schemadoc.cs`). The rules every writer follows are in `DATABASE_RULES.md`.

198 tables, 170 with a declared PRIMARY KEY, 340 relations in `GameDbSchema` (the database has no `REFERENCES`: SQLite does not enforce any of them). A *strict* relation has no dangling id in the original FM4; a *loose* one already dangles.

## Order of operations

Write (insert) the tables of level 0 first, then level 1, and so on: a row is written after the rows it points at. Remove in the opposite order: the rows that point at a row go before it. `DbIntegrity` refuses a write that leaves an id pointing at nothing, so a wrong order shows as an error, not as a silent break.

- **Level 0** (105): AffinityLevelDef, AIDiff, AIRubberbands, AISkillAdjustments, AITemperaments, ArcadeCarList, AuctionSearchValues, AutoSteerOverrides, BackfireLevels, BristolLayouts, CarClasses, CarDetails, CareerEventTypes, Combo_Engines, ContentOffers, ControllerLayouts, DamageDiffSettings, Data_CarBody, Demo_CarList, EnvironmentTimeOfDay, EventBonuses, EventGroups, EventScoring, EventShowroomChallenges, FlyingStartPositions, GameOptionCategories, GameOptionGroups, GameOptions, GameOptionValues, GraffitiPoints, HasCarExperience, KinectSplitscreenTracks, List_AeroPhysics, List_Aspiration, List_BadgeAndTitleUnlocks, List_BrakeType, List_CarType, List_ColorClass, List_ColorFinish, List_Cylinders, List_DamageModDef, List_DriveType, List_EngineConfig, List_EnginePlacement, List_FamilyBody, List_FamilyModel, List_FamilySpecial, List_MaterialType, List_PartAttribute, List_PartManufacturer, List_PartsStrings, List_Region, List_ShiftSystem, List_SpecialColors, List_SuspensionPhysicsType, List_SuspensionType, List_TireAffectCurve, List_TireFrictionCurve, List_TorqueCurve, List_Unlocks, List_UpgradeCarBodyTireWidthFront, List_UpgradeCarBodyTireWidthRear, List_UpgradeRimSizeFront, List_UpgradeRimSizeRear, List_VariableTiming, List_WheelStyle, Livery_Categories, Livery_Decals, Livery_VinylNames, Livery_VinylsDecals, MultiDiscContentPacks, MultiDiscSlides, NewProfile_BadgeTitleProgress, NewProfile_BadgeTitleUnlocks, NewProfile_Career_Affinity, NewProfile_Career_AIHistory, NewProfile_Career_CarGroups, NewProfile_Career_EventResults, NewProfile_Career_Garage, NewProfile_Career_RaceResults, NewProfile_Career_TrackUsage, NewProfile_CarExperienceRevealsSeen, NewProfile_CarExperienceUnlocks, NewProfile_CarPointsOfInterestProgress, NewProfile_ChangeTracking, NewProfile_ContentSeen, NewProfile_GameplayAggregate, NewProfile_LastUsedGarageId, PerformanceBudgetCameras, PittingProfiles, PlayerLevelDef, SplitPoints, StartGridPositions, StripeColors, TeamColors, ThumbnailTypes, TireMod_FrictionScale, TrackPittingZones, TrackSpecificAI, TrackStartingMerges, TrialLoadingImages, TurnSections, UpgradeAreas, VersionInfo, WheelLayouts
- **Level 1** (22): AIPlayerColors, AIRubberbandModeChoices, AISkills, Data_Drivetrain, Data_Engine, Data_Motor, EventRandomBonuses, GameOptionCategoryMap, GameOptionMap, List_Country, List_TireFrictionMultiCurve, List_UpgradeCarBodyChassisStiffness, List_UpgradeCarBodyFrontBumper, List_UpgradeCarBodyHood, List_UpgradeCarBodyRearBumper, List_UpgradeCarBodySideSkirt, List_UpgradeCarBodyWeight, List_Wheels, PerformanceBudget, Powertrains, UpgradeTypes, VolatileHostOptions
- **Level 2** (36): AILineChoices, AIPlayers, Environments, List_CarMake, List_TireCompound, List_UpgradeDrivetrainClutch, List_UpgradeDrivetrainDifferential, List_UpgradeDrivetrainDriveline, List_UpgradeDrivetrainTransmission, List_UpgradeEngineCamshaft, List_UpgradeEngineCSC, List_UpgradeEngineDisplacement, List_UpgradeEngineDSC, List_UpgradeEngineExhaust, List_UpgradeEngineFlywheel, List_UpgradeEngineFuelSystem, List_UpgradeEngineIgnition, List_UpgradeEngineIntake, List_UpgradeEngineIntercooler, List_UpgradeEngineManifold, List_UpgradeEngineOilCooling, List_UpgradeEnginePistonsCompression, List_UpgradeEngineRestrictorPlate, List_UpgradeEngineTurboQuad, List_UpgradeEngineTurboSingle, List_UpgradeEngineTurboTwin, List_UpgradeEngineValves, List_UpgradeMotorParts, MPAIDifficultyMap, NewProfile_Career_PurchasedParts, Upgrades, UpgradeWizardParts, UpgradeWizardPartsRemoval, UpgradeWizardPowerRemoval, WheelExceptions, WheelNormOffsets
- **Level 3** (4): Combo_TireBrandCompound, Data_Car, Livery_DecalsSortOrder, Tracks
- **Level 4** (24): CameraOverrides, CarExceptions, CarInvalidDefaultParts, CarListFilters, CarPartPositions, CarTrackOffsets, Combo_Colors, Combo_Colors_Exceptions, ContentOffersMapping, Events, HopperRaces, IECars, KinectSplitscreenCars, List_AntiSwayPhysics, List_SpringDamperPhysics, List_UpgradeBrakes, List_UpgradeCarBody, List_UpgradeDrivetrain, List_UpgradeEngine, List_UpgradeMotor, List_UpgradeRearWing, List_UpgradeTireCompound, Rewards_DriverLevelv3, Rewards_ShowroomUnlocks
- **Level 5** (6): EventParticipants, EventRestrictions, List_UpgradeAntiSwayFront, List_UpgradeAntiSwayRear, List_UpgradeSpringDamper, Races
- **Level 6** (1): RacesSpecial

A car (`Data_Car` row) is written in this order: the rows it points at must exist (`List_CarMake`, `CarClasses`, `Powertrains`, `List_FamilyModel`... levels 0 to 2), then `Data_Car` (level 3), then `Data_CarBody` with `Id` = car * 1000 (no relation in the schema: the formula links them, `DbIntegrity` checks it), then every table that has the car's Id as `Ordinal` / `CarId` (`List_Upgrade*`, `CarPartPositions`, `Combo_Colors`, the physics lists), whose own keys are `car * 1000 + n`. The string keys (`DisplayName`, `ModelShort`) are made first, in the `.str` tables. The port and Duplicate Car do this (`DbIds.RenumberCar`, `CarDuplicate`); `Combo_Engines` has a counter of its own (`EngineComboID`).

## Car columns

The columns that hold a car's Id (`DbIds.CarColumns`: by name `Ordinal`, `CarId`, `CarID`, `CarOrdinal`, plus every relation into `Data_Car.Id`):

- `ArcadeCarList.CarId`
- `CameraOverrides.CarId`
- `CarExceptions.CarID`
- `CarInvalidDefaultParts.Ordinal`
- `CarListFilters.CarID`
- `CarPartPositions.Ordinal`
- `Combo_Colors.Ordinal`
- `Combo_Colors_Exceptions.Ordinal`
- `Combo_Engines.Ordinal`
- `EventParticipants.Ordinal`
- `EventShowroomChallenges.CarID`
- `HasCarExperience.CarId`
- `IECars.CarId`
- `KinectSplitscreenCars.Ordinal`
- `List_AntiSwayPhysics.Ordinal`
- `List_SpringDamperPhysics.Ordinal`
- `List_UpgradeAntiSwayFront.Ordinal`
- `List_UpgradeAntiSwayRear.Ordinal`
- `List_UpgradeBrakes.Ordinal`
- `List_UpgradeCarBody.Ordinal`
- `List_UpgradeDrivetrain.Ordinal`
- `List_UpgradeEngine.Ordinal`
- `List_UpgradeMotor.Ordinal`
- `List_UpgradeRearWing.Ordinal`
- `List_UpgradeRimSizeFront.Ordinal`
- `List_UpgradeRimSizeRear.Ordinal`
- `List_UpgradeSpringDamper.Ordinal`
- `List_UpgradeTireCompound.Ordinal`
- `NewProfile_Career_Garage.CarId`
- `NewProfile_CarExperienceRevealsSeen.CarID`
- `NewProfile_CarExperienceUnlocks.CarID`
- `NewProfile_CarPointsOfInterestProgress.CarOrdinal`
- `StripeColors.Ordinal`
- `TireMod_FrictionScale.Ordinal`
- `CarTrackOffsets.Id`
- `Demo_CarList.Id`
- `Rewards_DriverLevelv3.RewardCarId`
- `Rewards_ShowroomUnlocks.RewardCarId`
- `ContentOffersMapping.ContentId`

## Tables

### AffinityLevelDef

51 rows, level 0. Key: `Id`.

Columns: Id INTEGER, XP INT, Discount REAL, BonusCredits INT

### AIDiff

3 rows, level 0. Key: `id`.

Columns: id INTEGER, DisplayName TEXT

Pointed at by: `TrackSpecificAI.AIDiff_id` (loose)

### AILineChoices

81 rows, level 2. Key: none.

Columns: AISkills_id INT, LineID INT, RelativeFrequency REAL

Points at: `AISkills_id` → `AISkills.id`

### AIPlayerColors

165 rows, level 1. Key: none.

Columns: PlayerId INT, PrimaryColorValid INT, PrimaryColor INT, IsMetallic INT, SpecialColorId INT

Points at: `SpecialColorId` → `List_SpecialColors.ID`; `PlayerId` → `AIPlayers.Id` (loose: colors of removed drivers)

### AIPlayers

121 rows, level 2. Key: `Id`.

Columns: Id INTEGER, Name TEXT, FirstName TEXT, LastName TEXT, CountryId INT, UISkill INT, Fee INT, HiredSkill INT, HiredAggro INT, SkillModifier INT, AggroModifier INT, DriverTypeID INT, Bio TEXT, Rank INT

Points at: `CountryId` → `List_Country.CountryID`; `FavoriteColorClassId` → `List_ColorClass.ColorClassId`

Pointed at by: `Data_Car.AIPlayerID` (loose), `AIPlayerColors.PlayerId` (loose), `EventParticipants.AIPlayerID`, `E3Drivers.AIPlayerId` (loose), `FirstTimeCareerDrivers.AIPlayerId` (loose), `FreeRoamDrivers.AIPlayerId` (loose)

### AIRubberbandModeChoices

22 rows, level 1. Key: `Id`.

Columns: Id INTEGER, AIRubberbands_id INT, AIRubberbandModes_id INT, Region INT, RelativeFrequency REAL, Amount REAL

Points at: `AIRubberbands_id` → `AIRubberbands.id`

### AIRubberbands

11 rows, level 0. Key: `id`.

Columns: id INTEGER, StartRubberbandingDistance REAL, MaxRubberbandingEffectDistance REAL, StartTorqueCutDistance REAL, MaxTorqueCutDistance REAL, TorqueCutFactor REAL

Pointed at by: `AISkills.Rubberbands_id`, `AIRubberbandModeChoices.AIRubberbands_id`, `Events.AIRubberbandEasy` (loose), `Events.AIRubberbandMed` (loose), `Events.AIRubberbandHard` (loose), `Events.AIRubberbandPro` (loose)

### AISkillAdjustments

21 rows, level 0. Key: none.

Columns: TimeRatio INT, SkillAdjustment INT

### AISkills

28 rows, level 1. Key: `id`.

Columns: id INTEGER, SoloLapping_OfflineTractionAssistFactor REAL, SoloLapping_BrakingMinimum REAL, SoloLapping_BrakingMaximum REAL, SoloLapping_CorneringMinimum REAL, SoloLapping_CorneringMaximum REAL, SoloLapping_DistBehindForMaxPerformance REAL, SoloLapping_NominalTorqueScale REAL, SoloLapping_NominalCheatLevel INT, SoloLapping_DistBehindForTorqueBoost REAL, SoloLapping_TorqueBoostCheatLevel INT, SoloLapping_FarBehindTorqueBoost REAL, Rubberbands_id INT

Points at: `Rubberbands_id` → `AIRubberbands.id`

Pointed at by: `Events.AISkillEasy`, `Events.AISkillMed`, `Events.AISkillHard`, `Events.AISkillPro`, `AILineChoices.AISkills_id`, `MPAIDifficultyMap.AISkill`

### AITemperaments

10 rows, level 0. Key: `id`.

Columns: id INTEGER, MiscDriver_Pass INT, Passing_AggressionLevel REAL, Passing_StartPassAtImpactTime REAL, Passing_TrailingDistance REAL, Passing_ScriptedPassProbability REAL, Passing_InteractionLinePassProbability REAL, Passing_LowRelVelPassProbability REAL, Passing_CarClearance REAL, Passing_TrackEdgeClearance REAL

Pointed at by: `Events.AITemperamentEasy`, `Events.AITemperamentMed`, `Events.AITemperamentHard`, `Events.AITemperamentPro`, `MPAIDifficultyMap.AITemperament`

### ArcadeCarList

215 rows, level 0. Key: `CarId`.

Columns: CarId INTEGER, Level INT

Points at: `CarId` → `Data_Car.Id` (loose: rows of cut cars)

### AuctionSearchValues

26 rows, level 0. Key: `ValueId`.

Columns: ValueId INTEGER, Min REAL, Max REAL

### AutoSteerOverrides

3 rows, level 0. Key: `Id`.

Columns: Id INTEGER, STMSlipMin REAL, STMSlipMax REAL, AutoSteerSTMSlipMin REAL, AutoSteerSTMSlipMax REAL

Pointed at by: `Data_Car.AutoSteerOverrideID`

### BackfireLevels

16 rows, level 0. Key: none.

Columns: ExhaustLevel INT, CamLevel INT, BackfireLevel INT

### BristolLayouts

5 rows, level 0. Key: `LayoutId`.

Columns: LayoutId INTEGER, LeftThumbUp TEXT, LeftThumbDown TEXT, LeftThumbButton TEXT, RightThumbUp TEXT, RightThumbDown TEXT, RightThumbLeft TEXT, RightThumbRight TEXT, RightThumbButton TEXT, DPadUp TEXT, DPadDown TEXT, DPadLeft TEXT, DPadRight TEXT, ButtonA TEXT, ButtonB TEXT, ButtonX TEXT, ButtonY TEXT, LeftShoulder TEXT, RightShoulder TEXT, LeftTrigger TEXT, RightTrigger TEXT

### CameraOverrides

503 rows, level 4. Key: `CarId`.

Columns: CarId INTEGER, CamFollowLowHeightOffset REAL, CamFollowLowRadiusOffset REAL, CamFollowLowTargetYOffset REAL, CamFollowHighHeightOffset REAL, CamFollowHighRadiusOffset REAL, CamFollowHighTargetYOffset REAL, CamBumperHighHeightOffset REAL, CamBumperHighZOffset REAL, CamBumperLowHeightOffset REAL, CamBumperLowZOffset REAL, CamHoodHeightOffset REAL, CamHoodZOffset REAL, CamHoodPitchOffset REAL, CamCockpitOffsetX REAL, CamCockpitOffsetY REAL, CamCockpitOffsetZ REAL, SplitscreenCamCockpitOffsetX REAL, SplitscreenCamCockpitOffsetY REAL, SplitscreenCamCockpitOffsetZ REAL, CockpitWheelPositionOffsetX REAL, CockpitWheelPositionOffsetY REAL, CockpitWheelPositionOffsetZ REAL, CockpitWheelRotationOffset REAL

Points at: `CarId` → `Data_Car.Id`

### CarClasses

11 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT, Description TEXT, CarImagePath TEXT, ClassColor INT, SecondaryColor INT, MaxPerformanceIndex REAL, MaxDisplayPerformanceIndex INT, BadgeTexturePathPrefix TEXT, CareerMinEligiblePI INT

Pointed at by: `Data_Car.ClassID`, `Events.TargetClass`

### CarDetails

33 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT, DisplayType TEXT, SortField TEXT, HelpString TEXT, SortOrder INT, Career INT, Arcade INT, Clan INT, FracDigits INT

### CareerEventTypes

32 rows, level 0. Key: `id`.

Columns: id INTEGER, Name TEXT, BannerId INT, RaceModeId INT, Description TEXT, IntroVO TEXT, PosterPath TEXT

Pointed at by: `Events.CareerTypeId`

### CarExceptions

485 rows, level 4. Key: `CarID`.

Columns: CarID INTEGER, NoMirrors INT, NoWindows INT, NoHood INT, NoPaintableWing INT

Points at: `CarID` → `Data_Car.Id`

### CarInvalidDefaultParts

0 rows, level 4. Key: none.

Columns: Ordinal INT, PartEnumValue INT

Points at: `Ordinal` → `Data_Car.Id`; `PartEnumValue` → `UpgradeTypes.PartEnum`

### CarListFilters

1 rows, level 4. Key: `CarID`.

Columns: CarID INTEGER, IsNullCar INT

Points at: `CarID` → `Data_Car.Id`

### CarPartPositions

5030 rows, level 4. Key: `ID (first column, not declared)`.

Columns: ID INT, Ordinal INT, PartId INT, PartPosX REAL, PartPosY REAL, PartPosZ REAL

Points at: `Ordinal` → `Data_Car.Id`

### CarTrackOffsets

170 rows, level 4. Key: `Id (first column, not declared)`.

Columns: Id INT, FrontOffset REAL, RearOffset REAL, UpgradeFrontOffset REAL, UpgradeRearOffset REAL

Points at: `Id` → `Data_Car.Id`

### Combo_Colors

2962 rows, level 4. Key: `ID`.

Columns: ID INTEGER, Ordinal INT, Sequence INT, Metallic INT, RGB INT, BaseTexture TEXT, DamageTexture TEXT, ColorClassId INT

Points at: `Ordinal` → `Data_Car.Id`; `ColorClassId` → `List_ColorClass.ColorClassId`

Pointed at by: `Combo_Colors_Exceptions.Ordinal,Sequence` (loose), `StripeColors.Ordinal,Sequence` (loose)

### Combo_Colors_Exceptions

425 rows, level 4. Key: `ID`.

Columns: ID INTEGER, Ordinal INT, Sequence INT, HasColor2 INT, Color2 INT, HasStripe INT, ColorStripe INT

Points at: `Ordinal` → `Data_Car.Id`; `Ordinal,Sequence` → `Combo_Colors.Ordinal,Sequence` (loose: one exception has no color)

### Combo_Engines

312 rows, level 0. Key: `EngineComboID`.

Columns: EngineComboID INTEGER, Ordinal INT, EngineID INT, Stock INT

Points at: `Ordinal` → `Data_Car.Id` (loose: old table, not used: List_UpgradeEngine holds the engines)

### Combo_TireBrandCompound

30 rows, level 3. Key: `Id`.

Columns: Id INTEGER, TireBrandId INT, TireCompoundId INT, PriceScale REAL, WearScale REAL, FrictionScale REAL, PeakSASRScale REAL, PriceDisplay INT, WearDisplay INT, FrictionDisplay INT, PeakSASRDisplay INT

Points at: `TireBrandId` → `List_PartManufacturer.Id`; `TireCompoundId` → `List_TireCompound.TireCompoundID`

### ContentOffers

4 rows, level 0. Key: `OfferId`.

Columns: ID TEXT, OfferId INTEGER, DisplayName TEXT, Description TEXT, PubOfferingID INT, LicenseLevel INT, LicenseMask INT, IsDLC INT, ImagePath TEXT, ThumbPath TEXT, IsInstalled INT, IsPurchased INT, IsVIP INT, Hidden INT, IsALaCarte INT, SeasonPassOffer BIGINT

Pointed at by: `ContentOffersMapping.OfferId`

### ContentOffersMapping

209 rows, level 4. Key: `Id`.

Columns: Id INTEGER, OfferId BIGINT, ContentId INT, ContentType INT

Points at: `ContentId` → `Data_Car.Id`; `OfferId` → `ContentOffers.OfferId`

### ControllerLayouts

13 rows, level 0. Key: `LayoutId`.

Columns: LayoutId INTEGER, LeftThumbUp TEXT, LeftThumbDown TEXT, LeftThumbButton TEXT, RightThumbUp TEXT, RightThumbDown TEXT, RightThumbLeft TEXT, RightThumbRight TEXT, RightThumbButton TEXT, DPadUp TEXT, DPadDown TEXT, DPadLeft TEXT, DPadRight TEXT, ButtonA TEXT, ButtonB TEXT, ButtonX TEXT, ButtonY TEXT, LeftShoulder TEXT, RightShoulder TEXT, LeftTrigger TEXT, RightTrigger TEXT

### DamageDiffSettings

3 rows, level 0. Key: `DamageDiff`.

Columns: DamageDiff INTEGER, AllowRolloverHuman INT, AllowRolloverAI INT, AllowDetachWheelHuman INT, AllowDetachWheelAI INT

### Data_Car

503 rows, level 3. Key: `Id`.

Columns: Id INTEGER, Year INT, MakeID INT, DisplayName TEXT, ModelShort TEXT, MediaName TEXT, ClassID INT, CarTypeID INT, CarClassModifier REAL, FamilyModelID INT, FamilyBodyID INT, EnginePlacementID INT, MaterialTypeID INT, PowertrainID INT, CurbWeight REAL, WeightDistribution REAL, NumGears INT, TireBrandID INT, BaseRarity REAL, FrontTireWidthMM INT, FrontTireAspect INT, FrontWheelDiameterIN INT, RearTireWidthMM INT, RearTireAspect INT, RearWheelDiameterIN INT, Time:0-60-sec REAL, Time:0-100-sec REAL, QuarterMileTime-sec REAL, QuarterMileSpeed-mph REAL, TopSpeed-mph REAL, SpeedLimiterID INT, BaseCost INT, PerformanceIndex REAL, SteerMaxAngle REAL, SteerMaxAngVelTurning REAL, SteerMaxAngVelStraighten REAL, SteerAngVelCountersteer REAL, SteerAngVelDynFindPeak REAL, SteerAccelTimeToMaxRate REAL, SteerSpeedSensitiveMaxGees REAL, SteerSpeedSensitiveMinMaxAngle REAL, SteerSpeedSensitiveSlowSpeed REAL, SteerSpeedSensitiveFastSpeed REAL, SteerSpeedSensitiveFastRateScale REAL, AssistsTCSSlipDefTakeoff REAL, AssistsTCSSlipDefMoving REAL, AutoSteerOverrideID INT, FixListingRearFricScale REAL, FixListingNormSlip0 REAL, FixListingNormSlip1 REAL, FixListingSteerAngle0 REAL, FixListingSteerAngle1 REAL, BodyAeroLongitudinalDrag REAL, BodyAeroVerticalDrag REAL, BodyAeroLateralDragFront REAL, BodyAeroLateralDragRear REAL, BodyAeroForwardDownforceFront REAL, BodyAeroForwardDownforceRear REAL, BodyAeroAngleZeroDownforce REAL, BodyAeroWIForceScale REAL, TireAeroHackShouldApply INT, TireAeroHackMinSpringLoad REAL, TireAeroHackMaxSpringLoad REAL, AIPlayerID INT, SimPeakPower REAL, SimPeakAngVel REAL, SimPeakTorque REAL, SimPeakTorqueAngVel REAL, SimRedlineAngVel REAL, SimTimeTo60MPH REAL, SimTimeTo100MPH REAL, SimTimeQuarterMile REAL, SimTopSpeed REAL, SimBrakeDistance100MPH REAL, SimLatGees60MPH REAL, SimLatGees120MPH REAL, SimSpeedQuarterMile REAL, SimBrakeDistance60MPH REAL, HandlingRating REAL, SpeedRating REAL, AccelerationRating REAL, BrakingRating REAL, GameTorqueScale REAL, GameDragScale REAL, CaliperAngleFront REAL, CaliperAngleRear REAL, Thumbnail TEXT, ContentId INT, IsPurchased INT, IsUnicorn INT, SteerMaxAngleFiltered REAL, HasLivery INT, WingMask INT, CaliperRGBFront INT, CaliperRGBRear INT, StockWheelID INT, IsArcade INT, IsInstalled INT, LaunchRating REAL, GaugeID INT, FamilySpecialID INT, EngineConfigID INT, DriveTypeID INT, RegionID INT, CountryID INT, MakeName TEXT, MakeIcon TEXT, CylinderID INT, AspirationTypeId INT, Displacement INT, ModelId INT, IsCarExperienceUnlocked INT, HasCarExperience INT, FrontStockRideHeight REAL, RearStockRideHeight REAL, IsDrivable INT, HasRaceLivery INT

Points at: `MakeID` → `List_CarMake.ID`; `ClassID` → `CarClasses.Id`; `CarTypeID` → `List_CarType.Id`; `FamilyModelID` → `List_FamilyModel.Id`; `FamilyBodyID` → `List_FamilyBody.Id`; `FamilySpecialID` → `List_FamilySpecial.FamilySpecialID`; `EnginePlacementID` → `List_EnginePlacement.ID`; `MaterialTypeID` → `List_MaterialType.MaterialTypeID`; `PowertrainID` → `Powertrains.Id`; `DriveTypeID` → `List_DriveType.ID`; `EngineConfigID` → `List_EngineConfig.ConfigID`; `CylinderID` → `List_Cylinders.CylinderID`; `AspirationTypeId` → `List_Aspiration.AspirationID`; `AutoSteerOverrideID` → `AutoSteerOverrides.Id`; `StockWheelID` → `List_Wheels.ID`; `RegionID` → `List_Region.Id`; `CountryID` → `List_Country.CountryID`; `AIPlayerID` → `AIPlayers.Id` (loose: 0 for every car); `GaugeID` → `List_DigitalGauges.GaugeID`; `TireBrandID` → `List_TireBrand.TireBrandID`

Pointed at by: `CarPartPositions.Ordinal`, `Combo_Colors.Ordinal`, `Combo_Colors_Exceptions.Ordinal`, `List_AntiSwayPhysics.Ordinal`, `List_SpringDamperPhysics.Ordinal`, `List_UpgradeAntiSwayFront.Ordinal`, `List_UpgradeAntiSwayRear.Ordinal`, `List_UpgradeBrakes.Ordinal`, `List_UpgradeCarBody.Ordinal`, `List_UpgradeDrivetrain.Ordinal`, `List_UpgradeEngine.Ordinal`, `List_UpgradeMotor.Ordinal`, `List_UpgradeRearWing.Ordinal`, `List_UpgradeSpringDamper.Ordinal`, `List_UpgradeTireCompound.Ordinal`, `EventParticipants.Ordinal`, `KinectSplitscreenCars.Ordinal`, `CarInvalidDefaultParts.Ordinal`, `List_UpgradeRimSizeFront.Ordinal` (loose), `List_UpgradeRimSizeRear.Ordinal` (loose), `StripeColors.Ordinal` (loose), `Combo_Engines.Ordinal` (loose), `CameraOverrides.CarId`, `CarExceptions.CarID`, `CarListFilters.CarID`, `CarTrackOffsets.Id`, `Demo_CarList.Id` (loose), `HasCarExperience.CarId` (loose), `TireMod_FrictionScale.Ordinal` (loose), `IECars.CarId`, `ArcadeCarList.CarId` (loose), `EventShowroomChallenges.CarID` (loose), `Rewards_DriverLevelv3.RewardCarId`, `Rewards_ShowroomUnlocks.RewardCarId`, `ContentOffersMapping.ContentId`, `NewProfile_Career_Garage.CarId` (loose), `NewProfile_CarExperienceUnlocks.CarID` (loose), `NewProfile_CarExperienceRevealsSeen.CarID` (loose), `NewProfile_CarPointsOfInterestProgress.CarOrdinal` (loose), `Rewards_DriverLevelv3.InitialCar`, `NewProfile_DLC_CarsSeen.CarID` (loose), `Events.PlayerCarId`, `CarPIOverrides.CarID` (loose), `CarUpgradeExceptions.CarID`, `EventRecommendedCars.CarID`, `E3Drivers.CarId`, `FirstTimeCareerDrivers.CarId`, `FreeRoamDrivers.CarId`, `Rewards_EventPrizes.RewardCarId`, `Rewards_LoyaltyCars.RewardCarId` (loose), `Rewards_Wristband.RewardCarId` (loose), `Data_Car_Buckets.CarId`, `DestinationEvents.PlayerCarId`, `Rewards_EventCars.CarId`, `BarnfindCars.Ordinal`, `ToggleActive.Ordinal`, `CarChallenges.CarId` (loose), `CarChallengeTitles.CarId` (loose), `OptionalRallyUpgrades.Ordinal` (loose)

### Data_CarBody

503 rows, level 0. Key: `Id`.

Columns: Id INTEGER, ModelWheelbase REAL, ModelFrontTrackOuter REAL, ModelRearTrackOuter REAL, ModelFrontStockRideHeight REAL, ModelRearStockRideHeight REAL, BottomCenterWheelbasePosx REAL, BottomCenterWheelbasePosy REAL, BottomCenterWheelbasePosZ REAL, PristineBoundingBoxMinX REAL, PristineBoundingBoxMinY REAL, PristineBoundingBoxMinZ REAL, PristineBoundingBoxMaxX REAL, PristineBoundingBoxMaxY REAL, PristineBoundingBoxMaxZ REAL

Pointed at by: `List_UpgradeCarBody.CarBodyID`, `List_UpgradeCarBodyChassisStiffness.CarBodyID`, `List_UpgradeCarBodyFrontBumper.CarBodyID`, `List_UpgradeCarBodyHood.CarBodyID`, `List_UpgradeCarBodyRearBumper.CarBodyID`, `List_UpgradeCarBodySideSkirt.CarBodyID`, `List_UpgradeCarBodyWeight.CarBodyID`, `List_UpgradeCarBodyTireWidthFront.CarBodyId` (loose), `List_UpgradeCarBodyTireWidthRear.CarBodyId` (loose)

### Data_Drivetrain

503 rows, level 1. Key: `DrivetrainID`.

Columns: DrivetrainID INTEGER, DrivetypeID INT, EngineMountingDirection INT

Points at: `DrivetypeID` → `List_DriveType.ID`

Pointed at by: `List_UpgradeDrivetrain.DrivetrainID`, `List_UpgradeDrivetrainClutch.DrivetrainID`, `List_UpgradeDrivetrainDifferential.DrivetrainID`, `List_UpgradeDrivetrainDriveline.DrivetrainID`, `List_UpgradeDrivetrainTransmission.DrivetrainID`

### Data_Engine

502 rows, level 1. Key: `EngineID`.

Columns: EngineID INTEGER, EngineMass-kg REAL, MediaName TEXT, ConfigID INT, CylinderID INT, Compression REAL, VariableTimingID INT, AspirationID_Stock INT, StockBoost-bar REAL, MomentInertia REAL, GasTankSize REAL, TorqueSteerLeftSpeedScale REAL, TorqueSteerRightSpeedScale REAL, EngineGraphingMaxTorque REAL, EngineGraphingMaxPower REAL, EngineName TEXT, EngineRotation INT, Carbureted INT, Diesel INT, Rotary INT

Points at: `ConfigID` → `List_EngineConfig.ConfigID`; `CylinderID` → `List_Cylinders.CylinderID`; `VariableTimingID` → `List_VariableTiming.VariableTimingID`; `AspirationID_Stock` → `List_Aspiration.AspirationID`

Pointed at by: `List_UpgradeEngine.EngineID`, `List_UpgradeEngineCSC.EngineID`, `List_UpgradeEngineCamshaft.EngineID`, `List_UpgradeEngineDSC.EngineID`, `List_UpgradeEngineDisplacement.EngineID`, `List_UpgradeEngineExhaust.EngineID`, `List_UpgradeEngineFlywheel.EngineID`, `List_UpgradeEngineFuelSystem.EngineID`, `List_UpgradeEngineIgnition.EngineID`, `List_UpgradeEngineIntake.EngineID`, `List_UpgradeEngineIntercooler.EngineID`, `List_UpgradeEngineManifold.EngineID`, `List_UpgradeEngineOilCooling.EngineID`, `List_UpgradeEnginePistonsCompression.EngineID`, `List_UpgradeEngineRestrictorPlate.EngineID`, `List_UpgradeEngineTurboQuad.EngineID`, `List_UpgradeEngineTurboSingle.EngineID`, `List_UpgradeEngineTurboTwin.EngineID`, `List_UpgradeEngineValves.EngineID`, `List_TorqueCurve.EngineID` (loose)

### Data_Motor

4 rows, level 1. Key: `MotorID`.

Columns: MotorID INTEGER, MotorMass-kg REAL, MediaName TEXT, MomentInertia REAL, MotorGraphingMaxTorque REAL, MotorGraphingMaxPower REAL, MotorName TEXT, RedlineRPM REAL, NumRPMEntriesArray INT, TorqueCurveFullThrottleID INT, BatteryCapacity REAL, ConfigID INT

Points at: `ConfigID` → `List_EngineConfig.ConfigID`; `TorqueCurveFullThrottleID` → `List_TorqueCurve.TorqueCurveID`

Pointed at by: `List_UpgradeMotor.MotorID`, `List_UpgradeMotorParts.MotorID`

### Demo_CarList

12 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DemoOrder INT

Points at: `Id` → `Data_Car.Id` (loose: 4 cut cars)

### Environments

39 rows, level 2. Key: `Id`.

Columns: Id INTEGER, Location TEXT, ImagePath TEXT, MediaName TEXT, DisplayName TEXT, ShortDisplayName TEXT, ThumbImageName TEXT, Description TEXT, CountryId INT, ShowOnMap INT, MapX REAL, MapY REAL

Points at: `CountryId` → `List_Country.CountryID`

Pointed at by: `Tracks.EnvironmentId`, `EnvironmentTimeOfDay.EnvironmentId` (loose)

### EnvironmentTimeOfDay

36 rows, level 0. Key: `Id`.

Columns: Id INTEGER, EnvironmentId INT, TimeOfDayIndex INT, Temperature INT, Description TEXT, Icon TEXT

Points at: `EnvironmentId` → `Environments.Id` (loose)

### EventBonuses

46 rows, level 0. Key: `Id`.

Columns: Id INTEGER, BonusTypeId INT, Amount INT

Pointed at by: `Events.BonusId`, `EventRandomBonuses.BonusId`

### EventGroups

18 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT

### EventParticipants

1682 rows, level 5. Key: `ID`.

Columns: ID INTEGER, EventID INT, Priority INT, Ordinal INT, ColorSequence INT, BucketId INT

Points at: `Ordinal` → `Data_Car.Id`; `EventID` → `Events.Id`; `AIPlayerID` → `AIPlayers.Id`

### EventRandomBonuses

81 rows, level 1. Key: none.

Columns: RandomBonusTypeId INT, BonusId INT, Weight INT, MinDriverLevel INT, MaxDriverLevel INT

Points at: `BonusId` → `EventBonuses.Id`

### EventRestrictions

340 rows, level 5. Key: `ID`.

Columns: ID INTEGER, EventID INT, BucketId INT, Value TEXT

Points at: `EventID` → `Events.Id`

### Events

315 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Name TEXT, Level INT, CareerTypeId INT, Description TEXT, NumberOfRaces INT, PittingProfileID INT, CrowdSize REAL, CrowdExcitement REAL, NumberOfDrivers INT, CashPrize INT, IsDLC INT, EventOrder INT, GroupMask INT, TargetClass INT, AISkillEasy INT, AISkillMed INT, AISkillHard INT, AISkillPro INT, AITemperamentEasy INT, AITemperamentMed INT, AITemperamentHard INT, AITemperamentPro INT, BonusId INT, CanDupPlayerCar INT

Points at: `CareerTypeId` → `CareerEventTypes.id`; `PittingProfileID` → `PittingProfiles.id`; `TargetClass` → `CarClasses.Id`; `BonusId` → `EventBonuses.Id`; `Level` → `PlayerLevelDef.Id`; `AISkillEasy` → `AISkills.id`; `AITemperamentEasy` → `AITemperaments.id`; `AISkillMed` → `AISkills.id`; `AITemperamentMed` → `AITemperaments.id`; `AISkillHard` → `AISkills.id`; `AITemperamentHard` → `AITemperaments.id`; `AISkillPro` → `AISkills.id`; `AITemperamentPro` → `AITemperaments.id`; `GameTypeId` → `GameTypes.id`; `ScoringID` → `EventScoring.ScoringID`; `PlayerCarId` → `Data_Car.Id`; `HubId` → `EventHubs.Id`; `TimeOfDayDescriptionID` → `TimeOfDayDescriptions.Id`; `CampaignId` → `Campaigns.Id`; `AIRubberbandEasy` → `AIRubberbands.id` (loose: FH1 uses 14); `AIRubberbandMed` → `AIRubberbands.id` (loose: FH1 uses 14); `AIRubberbandHard` → `AIRubberbands.id` (loose: FH1 uses 14); `AIRubberbandPro` → `AIRubberbands.id` (loose: FH1 uses 14)

Pointed at by: `Races.EventId`, `EventParticipants.EventID`, `EventRestrictions.EventID`, `EventShowroomChallenges.EventID` (loose), `NewProfile_Career_EventResults.EventId` (loose), `AES_Tip_Overrides.EventId`, `ChampSeries.EventId`, `EventHubInitialEvents.EventId`, `EventRecommendedCars.EventID`, `EventUIColors.EventId` (loose), `Event_Music.EventId`, `Rewards_EventPrizes.EventId`, `Rewards_EventUnlock.EventId` (loose), `Rewards_EventUnlock.UnlockEventId`, `CampaignEvents.EventId`, `DestinationEvents.EventId`, `MultiStageSeriesEvents.EventId`, `MultiStageSeriesRaces.EventId`, `Rewards_EventCars.EventId`

### EventScoring

8 rows, level 0. Key: `ID`.

Columns: ID INTEGER, ScoringID INT, place INT, Credits INT

Pointed at by: `Events.ScoringID`

### EventShowroomChallenges

26 rows, level 0. Key: `CarID`.

Columns: CarID INTEGER, EventID INT

Points at: `CarID` → `Data_Car.Id` (loose); `EventID` → `Events.Id` (loose)

### FlyingStartPositions

52 rows, level 0. Key: `id`.

Columns: id INTEGER, PosX REAL, PosY REAL, PosZ REAL, HeadingX REAL, HeadingY REAL, HeadingZ REAL, Speed REAL

Pointed at by: `Tracks.FlyingStartPosId` (loose)

### GameOptionCategories

19 rows, level 0. Key: `Id`.

Columns: Id INTEGER, CategoryName TEXT, DisplayName TEXT

Pointed at by: `GameOptionCategoryMap.CatId` (loose), `GameOptionGroupMap.CatId`

### GameOptionCategoryMap

391 rows, level 1. Key: none.

Columns: GroupId INT, CatId INT, OptionId INT, DefValue INT, isVisible INT, isLocked INT

Points at: `GroupId` → `GameOptionGroups.Id`; `CatId` → `GameOptionCategories.Id` (loose); `OptionId` → `GameOptions.Id` (loose)

### GameOptionGroups

17 rows, level 0. Key: `Id`.

Columns: Id INTEGER, GroupName TEXT

Pointed at by: `GameOptionCategoryMap.GroupId`, `GameOptionGroupMap.GroupId`

### GameOptionMap

253 rows, level 1. Key: none.

Columns: OptionId INT, ValueId INT

Points at: `OptionId` → `GameOptions.Id` (loose); `ValueId` → `GameOptionValues.Id`

### GameOptions

123 rows, level 0. Key: `Id (first column, not declared)`.

Columns: Id INT, DisplayName TEXT, Token TEXT, isProperty INT, xblValue INT, Query TEXT, NumMin INT, NumMax INT

Pointed at by: `GameOptionCategoryMap.OptionId` (loose), `GameOptionMap.OptionId` (loose), `VolatileHostOptions.OptionId`

### GameOptionValues

148 rows, level 0. Key: `Id (first column, not declared)`.

Columns: Id INT, DisplayName TEXT, Token TEXT, Value INT, Clause TEXT

Pointed at by: `GameOptionMap.ValueId`

### GraffitiPoints

2063 rows, level 0. Key: `ID`.

Columns: ID INTEGER, TrackId INT, Split REAL, DriftScalar REAL

Points at: `TrackId` → `Tracks.id` (loose: cut layouts)

### HasCarExperience

26 rows, level 0. Key: `CarId`.

Columns: CarId INTEGER

Points at: `CarId` → `Data_Car.Id` (loose: one cut car)

### HopperRaces

20 rows, level 4. Key: `Id`.

Columns: Id INTEGER, TrackId INT, NumLaps INT

Points at: `TrackId` → `Tracks.id`

### IECars

8 rows, level 4. Key: `CarId`.

Columns: CarId INTEGER, SortOrder INT

Points at: `CarId` → `Data_Car.Id`

### KinectSplitscreenCars

40 rows, level 4. Key: none.

Columns: Ordinal INT

Points at: `Ordinal` → `Data_Car.Id`

### KinectSplitscreenTracks

30 rows, level 0. Key: none.

Columns: TrackId INT

Points at: `TrackId` → `Tracks.id` (loose)

### List_AeroPhysics

427 rows, level 0. Key: `AeroPhysicsID`.

Columns: AeroPhysicsID INTEGER, DefaultTuneSlider REAL, Drag0 REAL, Downforce0 REAL, Drag1 REAL, Downforce1 REAL, AngleZeroDownforce REAL, LateralDrag REAL, DFTorqueScaleOneSliderInput0 REAL, DFTorqueScaleOneSliderInput1 REAL, DFTorqueScaleOneSliderInput2 REAL, DFTorqueScaleOneTorqueScaleOutput0 REAL, DFTorqueScaleOneTorqueScaleOutput1 REAL, DFTorqueScaleOneTorqueScaleOutput2 REAL, DFTorqueScaleTwoSliderInput0 REAL, DFTorqueScaleTwoSliderInput1 REAL, DFTorqueScaleTwoSliderInput2 REAL, DFTorqueScaleTwoTorqueScaleOutput0 REAL, DFTorqueScaleTwoTorqueScaleOutput1 REAL, DFTorqueScaleTwoTorqueScaleOutput2 REAL

Pointed at by: `List_UpgradeCarBodyFrontBumper.AeroPhysicsID`, `List_UpgradeRearWing.AeroPhysicsID`

### List_AntiSwayPhysics

2470 rows, level 4. Key: `AntiSwayPhysicsID`.

Columns: AntiSwayPhysicsID INTEGER, Ordinal INT, DefSwaybarStiffness REAL, MinSwaybarStiffness REAL, MaxSwaybarStiffness REAL, SwaybarDamping REAL

Points at: `Ordinal` → `Data_Car.Id`

Pointed at by: `List_UpgradeAntiSwayFront.AntiSwayPhysicsID`, `List_UpgradeAntiSwayRear.AntiSwayPhysicsID`

### List_Aspiration

8 rows, level 0. Key: `AspirationID`.

Columns: AspirationID INTEGER, Aspiration TEXT, DisplayName TEXT, ShortDisplayName TEXT, KeyPartEnum INT

Points at: `KeyPartEnum` → `UpgradeTypes.PartEnum` (loose: -1 for none)

Pointed at by: `Data_Car.AspirationTypeId`, `Data_Engine.AspirationID_Stock`

### List_BadgeAndTitleUnlocks

460 rows, level 0. Key: `UnlockId`.

Columns: UnlockId INTEGER, UnlockCategory INT, UnlockLevel INT, UnlockName TEXT, UnlockDescription TEXT, UnlockBadgeTexture TEXT, BadgeSelectionTexture TEXT, UnlockTitleString TEXT, HasBadge INT, HasTitle INT, SortOrder INT, IsSecret INT

### List_BrakeType

5 rows, level 0. Key: `BrakeTypeID`.

Columns: BrakeTypeID INTEGER, BrakeType TEXT, PartName TEXT

Pointed at by: `List_UpgradeBrakes.FrontBrakeTypeID`, `List_UpgradeBrakes.RearBrakeTypeID`

### List_CarMake

79 rows, level 2. Key: `ID`.

Columns: ID INTEGER, DisplayName TEXT, ManufacturerCode TEXT, CountryID INT, IconPath TEXT, IconPathLight TEXT, IconPathBase TEXT, Profile TEXT

Points at: `CountryID` → `List_Country.CountryID`

Pointed at by: `Data_Car.MakeID`, `List_UpgradeEngine.ManufacturerID` (loose), `List_UpgradeDrivetrain.ManufacturerId`, `Livery_DecalsSortOrder.MakeID`, `Rewards_CarPartDiscounts.CarMakeId`

### List_CarType

3 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT

Pointed at by: `Data_Car.CarTypeID`

### List_ColorClass

24 rows, level 0. Key: `ColorClassId`.

Columns: ColorClassId INTEGER, ColorClass TEXT, UIColor INT

Pointed at by: `Combo_Colors.ColorClassId`, `AIPlayers.FavoriteColorClassId`

### List_ColorFinish

3 rows, level 0. Key: `ColorFinishID`.

Columns: ColorFinishID INTEGER, ColorFinish TEXT

### List_Country

22 rows, level 1. Key: `CountryID`.

Columns: CountryID INTEGER, Country TEXT, RegionID INT, IconPath TEXT

Points at: `RegionID` → `List_Region.Id`

Pointed at by: `Data_Car.CountryID`, `List_CarMake.CountryID`, `Environments.CountryId`, `AIPlayers.CountryId`

### List_Cylinders

12 rows, level 0. Key: `CylinderID`.

Columns: CylinderID INTEGER, Number INT, Description TEXT, Rotors INT

Pointed at by: `Data_Car.CylinderID`, `Data_Engine.CylinderID`

### List_DamageModDef

11 rows, level 0. Key: `DamageModDefID`.

Columns: DamageModDefID INTEGER, DamageModName TEXT, ModZeroDamage REAL, ModFullDamage REAL, DamageMinImpact REAL, DamageMaxImpact REAL, DamageMin REAL, DamageMax REAL, RandomImpact REAL, ProbabilityMinImpact REAL, ProbabilityMaxImpact REAL, ProbabilityMin REAL, ProbabilityMax REAL

Pointed at by: `List_UpgradeSpringDamper.SuspensionDamageModDefID`, `List_UpgradeSpringDamper.TiresDamageModDefID`, `List_UpgradeBrakes.DamageModDefID`

### List_DriveType

3 rows, level 0. Key: `ID`.

Columns: ID INTEGER, DriveType TEXT, DisplayName TEXT

Pointed at by: `Data_Car.DriveTypeID`, `Data_Drivetrain.DrivetypeID`, `Powertrains.DrivetrainId`

### List_EngineConfig

9 rows, level 0. Key: `ConfigID`.

Columns: ConfigID INTEGER, EngineConfig TEXT, DisplayName TEXT

Pointed at by: `Data_Car.EngineConfigID`, `Data_Engine.ConfigID`, `Data_Motor.ConfigID`

### List_EnginePlacement

3 rows, level 0. Key: `ID`.

Columns: ID INTEGER, EnginePlacement TEXT, DisplayName TEXT

Pointed at by: `Data_Car.EnginePlacementID`, `Powertrains.EnginePlacementId` (loose)

### List_FamilyBody

22 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT

Pointed at by: `Data_Car.FamilyBodyID`

### List_FamilyModel

37 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT

Pointed at by: `Data_Car.FamilyModelID`

### List_FamilySpecial

5 rows, level 0. Key: `FamilySpecialID`.

Columns: FamilySpecialID INTEGER, DisplayName TEXT

Pointed at by: `Data_Car.FamilySpecialID`

### List_MaterialType

2 rows, level 0. Key: `MaterialTypeID`.

Columns: MaterialTypeID INTEGER, Material TEXT, PhysicsMaterialID INT

Pointed at by: `Data_Car.MaterialTypeID`

### List_PartAttribute

546 rows, level 0. Key: `PartAttributeID`.

Columns: PartAttributeID INTEGER, ManufacturerID INT, Price INT, Mass REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `ManufacturerID` → `List_PartManufacturer.Id` (loose: old table, not used)

### List_PartManufacturer

569 rows, level 0. Key: `Id`.

Columns: Id INTEGER, PartManufacturer TEXT, LogoPath TEXT

Pointed at by: `List_UpgradeAntiSwayFront.ManufacturerID`, `List_UpgradeAntiSwayRear.ManufacturerID`, `List_UpgradeBrakes.ManufacturerID`, `List_UpgradeCarBody.ManufacturerID`, `List_UpgradeCarBodyChassisStiffness.ManufacturerID`, `List_UpgradeCarBodyFrontBumper.ManufacturerID`, `List_UpgradeCarBodyHood.ManufacturerID`, `List_UpgradeCarBodyRearBumper.ManufacturerID`, `List_UpgradeCarBodySideSkirt.ManufacturerID`, `List_UpgradeCarBodyWeight.ManufacturerID`, `List_UpgradeDrivetrainClutch.ManufacturerID`, `List_UpgradeDrivetrainDifferential.ManufacturerID`, `List_UpgradeDrivetrainDriveline.ManufacturerID`, `List_UpgradeDrivetrainTransmission.ManufacturerID`, `List_UpgradeEngineCSC.ManufacturerID`, `List_UpgradeEngineCamshaft.ManufacturerID`, `List_UpgradeEngineDSC.ManufacturerID`, `List_UpgradeEngineDisplacement.ManufacturerID`, `List_UpgradeEngineExhaust.ManufacturerID`, `List_UpgradeEngineFlywheel.ManufacturerID`, `List_UpgradeEngineFuelSystem.ManufacturerID`, `List_UpgradeEngineIgnition.ManufacturerID`, `List_UpgradeEngineIntake.ManufacturerID`, `List_UpgradeEngineIntercooler.ManufacturerID`, `List_UpgradeEngineManifold.ManufacturerID`, `List_UpgradeEngineOilCooling.ManufacturerID`, `List_UpgradeEnginePistonsCompression.ManufacturerID`, `List_UpgradeEngineRestrictorPlate.ManufacturerID`, `List_UpgradeEngineTurboQuad.ManufacturerID`, `List_UpgradeEngineTurboSingle.ManufacturerID`, `List_UpgradeEngineTurboTwin.ManufacturerID`, `List_UpgradeEngineValves.ManufacturerID`, `List_UpgradeMotor.ManufacturerID`, `List_UpgradeMotorParts.ManufacturerID`, `List_UpgradeRearWing.ManufacturerID`, `List_UpgradeSpringDamper.ManufacturerID`, `List_UpgradeTireCompound.ManufacturerID`, `List_PartAttribute.ManufacturerID` (loose), `List_Wheels.PartManufacturerID`, `Combo_TireBrandCompound.TireBrandId`, `Rewards_CarPartDiscounts.BrandId`

### List_PartsStrings

136 rows, level 0. Key: `Id`.

Columns: Id INTEGER, PartsString TEXT

Pointed at by: `List_UpgradeCarBodyChassisStiffness.PartsStringID`, `List_UpgradeCarBodyFrontBumper.PartsStringID`, `List_UpgradeCarBodyHood.PartsStringID`, `List_UpgradeCarBodyRearBumper.PartsStringID`, `List_UpgradeCarBodySideSkirt.PartsStringID`, `List_UpgradeCarBodyWeight.PartsStringID`, `List_UpgradeEngineCSC.PartsStringID`, `List_UpgradeEngineDSC.PartsStringID`, `List_UpgradeEngineExhaust.PartsStringID`, `List_UpgradeEngineFuelSystem.PartsStringID`, `List_UpgradeEngineIgnition.PartsStringID`, `List_UpgradeEngineIntake.PartsStringID`, `List_UpgradeEngineIntercooler.PartsStringID`, `List_UpgradeRearWing.PartsStringID`

### List_Region

3 rows, level 0. Key: `Id`.

Columns: Id INTEGER, DisplayName TEXT

Pointed at by: `Data_Car.RegionID`, `List_Country.RegionID`

### List_ShiftSystem

7 rows, level 0. Key: `ShiftSystemID`.

Columns: ShiftSystemID INTEGER, ShiftSystem TEXT

### List_SpecialColors

102 rows, level 0. Key: `ID`.

Columns: ID INTEGER, DisplayName TEXT, Finish INT, PriColorValid INT, PriColorRGB INT, SecColorValid INT, SecColorRGB INT, TwoToneScale REAL, TwoToneBias REAL, TwoTonePower REAL

Pointed at by: `AIPlayerColors.SpecialColorId`

### List_SpringDamperPhysics

2686 rows, level 4. Key: `SpringDamperPhysicsID`.

Columns: SpringDamperPhysicsID INTEGER, Ordinal INT, SuspensionPhysicsTypeID INT, DefRideHeight REAL, MinRideHeight REAL, MaxRideHeight REAL, MaxCompressHeight REAL, DefSpringRate REAL, MinSpringRate REAL, MaxSpringRate REAL, DefDampenBumpRate REAL, MinDampenBumpRate REAL, MaxDampenBumpRate REAL, DampenBumpClamp REAL, DefDampenReboundRate REAL, MinDampenReboundRate REAL, MaxDampenReboundRate REAL, DampenReboundClamp REAL, BumpstopStiffness REAL, BumpstopDamping REAL, ChassisPivotX REAL, ChassisPivotY REAL, TirePivotX REAL, CamberChangeRatioCompression REAL, CamberChangeRatioExpansion REAL, StaticToe REAL, StaticCamber REAL, Caster REAL, SteerRotAxisTireOffsetX REAL, YMaxSpringVel REAL, MaxStretchDeltaFromRideHeight REAL

Points at: `Ordinal` → `Data_Car.Id`; `SuspensionPhysicsTypeID` → `List_SuspensionPhysicsType.SuspensionPhysicsTypeID`

Pointed at by: `List_UpgradeSpringDamper.FrontSpringDamperPhysicsID`, `List_UpgradeSpringDamper.RearSpringDamperPhysicsID`

### List_SuspensionPhysicsType

2 rows, level 0. Key: none.

Columns: SuspensionPhysicsTypeID INT, Name TEXT

Pointed at by: `List_SpringDamperPhysics.SuspensionPhysicsTypeID`

### List_SuspensionType

6 rows, level 0. Key: `SuspensionTypeID`.

Columns: SuspensionTypeID INTEGER, SuspensionType TEXT

### List_TireAffectCurve

108 rows, level 0. Key: `AffectCurveID`.

Columns: AffectCurveID INTEGER, NumValues INT, MinInput REAL, MaxInput REAL, OutputScale REAL, v0 REAL, v1 REAL, v2 REAL, v3 REAL, v4 REAL, v5 REAL, v6 REAL, v7 REAL, v8 REAL, v9 REAL, v10 REAL, v11 REAL, v12 REAL, v13 REAL, v14 REAL, v15 REAL, v16 REAL, v17 REAL, v18 REAL, v19 REAL, v20 REAL

Pointed at by: `List_TireCompound.AffectCurveTempAffectFrictionID`, `List_TireCompound.AffectCurveTempAffectWearID`, `List_TireCompound.AffectCurvePressureAffectFrictionID`, `List_TireCompound.AffectCurvePressureAffectWearID`, `List_TireCompound.AffectCurveStartPressureAffectPeakSASRID`, `List_TireCompound.AffectCurveCamberAffectLatFrictionID`, `List_TireCompound.AffectCurveCamberAffectLongFrictionID`, `List_TireCompound.AffectCurveWearAffectFrictionID`, `List_TireCompound.AffectCurveSpeedAffectFrictionID`, `List_TireCompound.AffectCurveNormSlipAngleAffectAlignTorqueID`, `List_TireCompound.AffectCurveNormSlipRatioAffectAlignTorqueID`, `List_TireCompound.AffectCurveLoadAffectAlignTorqueID`

### List_TireCompound

9 rows, level 2. Key: `TireCompoundID`.

Columns: TireCompoundID INTEGER, DisplayName TEXT, TorqueFreeLatFrictionScale REAL, TorqueFreeLongFrictionScaleBrake REAL, TorqueFreeLongFrictionScaleAccel0 REAL, TorqueFreeLongFrictionScaleAccel1 REAL, TorqueFreeLongFrictionScaleAccelSpeed0 REAL, TorqueFreeLongFrictionScaleAccelSpeed1 REAL, WetFrictionModFrictionScale REAL, WetFrictionModSlipScale REAL, MomentInertia REAL, TireFricWidth0 REAL, TireFricWidth1 REAL, TireFricScale0 REAL, TireFricScale1 REAL, AirTempDiffHeatTransfer REAL, AirTempDiffHeatTransferOverheated REAL, OverheatedTemp REAL, AirSpeedToDoubleHeatTransfer REAL, RollResistanceHeatTransfer REAL, FrictionHeatTransfer REAL, FrictionWear REAL, RelTireSpeedToDoubleHeatTransfer REAL, LoadToDoubleHeatTransfer REAL, HeatSlipMagToSlipPercentIn0 REAL, HeatSlipMagToSlipPercentOut0 REAL, HeatSlipMagToSlipPercentIn1 REAL, HeatSlipMagToSlipPercentOut1 REAL, HeatSlipMagToSlipPercentIn2 REAL, HeatSlipMagToSlipPercentOut2 REAL, PressureChangeSpeed REAL, SlidingFrictionHeatTransfer REAL, SlidingOffsetCooling REAL, SlidingOffsetOverheatedTemp REAL, SlidingOffsetCoolingOverheated REAL, SlidingSlipIn0 REAL, SlidingSlipOut0 REAL, SlidingSlipIn1 REAL, SlidingSlipOut1 REAL, SlidingOffsetAddToTemp REAL, SlidingOffsetAddToTempClamp REAL, CamberAffectOutsideHeatIn REAL, CamberAffectOutsideHeatOut REAL, HUDTemps0 REAL, HUDTemps1 REAL, HUDTemps2 REAL, HUDTemps3 REAL, FrictionMultiCurveLateralID INT, FrictionMultiCurveLongitudinalID INT, AffectCurveTempAffectFrictionID INT, AffectCurveTempAffectWearID INT, AffectCurvePressureAffectFrictionID INT, AffectCurvePressureAffectWearID INT, AffectCurveStartPressureAffectPeakSASRID INT, AffectCurveCamberAffectLatFrictionID INT, AffectCurveCamberAffectLongFrictionID INT, AffectCurveWearAffectFrictionID INT, AffectCurveSpeedAffectFrictionID INT, AffectCurveNormSlipAngleAffectAlignTorqueID INT, AffectCurveNormSlipRatioAffectAlignTorqueID INT, AffectCurveLoadAffectAlignTorqueID INT

Points at: `FrictionMultiCurveLateralID` → `List_TireFrictionMultiCurve.FrictionMultiCurveID`; `FrictionMultiCurveLongitudinalID` → `List_TireFrictionMultiCurve.FrictionMultiCurveID`; `AffectCurveTempAffectFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveTempAffectWearID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurvePressureAffectFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurvePressureAffectWearID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveStartPressureAffectPeakSASRID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveCamberAffectLatFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveCamberAffectLongFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveWearAffectFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveSpeedAffectFrictionID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveNormSlipAngleAffectAlignTorqueID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveNormSlipRatioAffectAlignTorqueID` → `List_TireAffectCurve.AffectCurveID`; `AffectCurveLoadAffectAlignTorqueID` → `List_TireAffectCurve.AffectCurveID`

Pointed at by: `List_UpgradeTireCompound.TireCompoundID`, `Combo_TireBrandCompound.TireCompoundId`

### List_TireFrictionCurve

36 rows, level 0. Key: `FrictionCurveID`.

Columns: FrictionCurveID INTEGER, NumCurveValues INT, FrictionScale REAL, v0 REAL, v1 REAL, v2 REAL, v3 REAL, v4 REAL, v5 REAL, v6 REAL, v7 REAL, v8 REAL, v9 REAL, v10 REAL, v11 REAL, v12 REAL, v13 REAL, v14 REAL, v15 REAL, v16 REAL, v17 REAL, v18 REAL, v19 REAL, v20 REAL, v21 REAL, v22 REAL, v23 REAL, v24 REAL, v25 REAL, v26 REAL, v27 REAL, v28 REAL, v29 REAL, v30 REAL, v31 REAL, v32 REAL, v33 REAL, v34 REAL, v35 REAL, v36 REAL, v37 REAL, v38 REAL, v39 REAL, v40 REAL, v41 REAL, v42 REAL, v43 REAL, v44 REAL, v45 REAL, v46 REAL, v47 REAL, v48 REAL, v49 REAL, v50 REAL, v51 REAL, v52 REAL, v53 REAL, v54 REAL, v55 REAL, v56 REAL, v57 REAL, v58 REAL, v59 REAL, v60 REAL, v61 REAL, v62 REAL, v63 REAL, v64 REAL, v65 REAL, v66 REAL, v67 REAL, v68 REAL, v69 REAL, v70 REAL, v71 REAL, v72 REAL, v73 REAL, v74 REAL, v75 REAL, v76 REAL, v77 REAL, v78 REAL, v79 REAL, v80 REAL, v81 REAL, v82 REAL, v83 REAL, v84 REAL, v85 REAL, v86 REAL, v87 REAL, v88 REAL, v89 REAL, v90 REAL, v91 REAL, v92 REAL, v93 REAL, v94 REAL, v95 REAL, v96 REAL, v97 REAL, v98 REAL, v99 REAL

Pointed at by: `List_TireFrictionMultiCurve.TireFrictionCurveID0`, `List_TireFrictionMultiCurve.TireFrictionCurveID1`

### List_TireFrictionMultiCurve

18 rows, level 1. Key: `FrictionMultiCurveID`.

Columns: FrictionMultiCurveID INTEGER, MaxSlip REAL, MinLoadCurve REAL, MaxLoadCurve REAL, LoadClamp REAL, TireFrictionCurveID0 INT, TireFrictionCurveID1 INT

Points at: `TireFrictionCurveID0` → `List_TireFrictionCurve.FrictionCurveID`; `TireFrictionCurveID1` → `List_TireFrictionCurve.FrictionCurveID`

Pointed at by: `List_TireCompound.FrictionMultiCurveLateralID`, `List_TireCompound.FrictionMultiCurveLongitudinalID`

### List_TorqueCurve

1422 rows, level 0. Key: `TorqueCurveID`.

Columns: TorqueCurveID INTEGER, TorqueScale REAL, NumTorqueValues INT, v0 REAL, v1 REAL, v2 REAL, v3 REAL, v4 REAL, v5 REAL, v6 REAL, v7 REAL, v8 REAL, v9 REAL, v10 REAL, v11 REAL, v12 REAL, v13 REAL, v14 REAL, v15 REAL, v16 REAL, v17 REAL, v18 REAL, v19 REAL, v20 REAL, v21 REAL, v22 REAL, v23 REAL, v24 REAL, v25 REAL, v26 REAL, v27 REAL, v28 REAL, v29 REAL, v30 REAL, v31 REAL, v32 REAL, v33 REAL, v34 REAL, v35 REAL, v36 REAL, v37 REAL, v38 REAL, v39 REAL, v40 REAL, v41 REAL, v42 REAL, v43 REAL, v44 REAL, v45 REAL, v46 REAL, v47 REAL, v48 REAL, v49 REAL, v50 REAL, v51 REAL, v52 REAL, v53 REAL, v54 REAL, v55 REAL, v56 REAL, v57 REAL, v58 REAL, v59 REAL, v60 REAL, v61 REAL, v62 REAL, v63 REAL, v64 REAL, v65 REAL, v66 REAL, v67 REAL, v68 REAL, v69 REAL, v70 REAL, v71 REAL, v72 REAL, v73 REAL, v74 REAL, v75 REAL, v76 REAL, v77 REAL, v78 REAL, v79 REAL, v80 REAL, v81 REAL, v82 REAL, v83 REAL, v84 REAL, v85 REAL, v86 REAL, v87 REAL, v88 REAL, v89 REAL, v90 REAL, v91 REAL, v92 REAL, v93 REAL, v94 REAL, v95 REAL, v96 REAL, v97 REAL, v98 REAL, v99 REAL, v100 REAL, v101 REAL, v102 REAL, v103 REAL, v104 REAL, v105 REAL, v106 REAL, v107 REAL, v108 REAL, v109 REAL, v110 REAL, v111 REAL, v112 REAL, v113 REAL, v114 REAL, v115 REAL, v116 REAL, v117 REAL, v118 REAL, v119 REAL, v120 REAL, v121 REAL, v122 REAL, v123 REAL, v124 REAL, v125 REAL, v126 REAL, v127 REAL, v128 REAL, v129 REAL, v130 REAL, v131 REAL, v132 REAL, v133 REAL, v134 REAL, v135 REAL, v136 REAL, v137 REAL, v138 REAL, v139 REAL, v140 REAL, v141 REAL, v142 REAL, v143 REAL, v144 REAL, v145 REAL, v146 REAL, v147 REAL, v148 REAL, v149 REAL, v150 REAL, v151 REAL, v152 REAL, v153 REAL, v154 REAL, v155 REAL, v156 REAL, v157 REAL, v158 REAL, v159 REAL, v160 REAL, v161 REAL, v162 REAL, v163 REAL, v164 REAL, v165 REAL, v166 REAL, v167 REAL, v168 REAL, v169 REAL, v170 REAL, v171 REAL, v172 REAL, v173 REAL, v174 REAL, v175 REAL, v176 REAL, v177 REAL, v178 REAL, v179 REAL, v180 REAL, v181 REAL, v182 REAL, v183 REAL, v184 REAL, v185 REAL, ZeroThrottleTorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID` (loose: FM3: 16 curves of cut engines)

Pointed at by: `Data_Motor.TorqueCurveFullThrottleID`, `List_UpgradeEngineCamshaft.TorqueCurveFullThrottleID`, `List_UpgradeEngineCamshaft.TorqueCurveZeroThrottleID`

### List_Unlocks

4 rows, level 0. Key: `Id`.

Columns: Id INTEGER, UnlockState TEXT

### List_UpgradeAntiSwayFront

1223 rows, level 5. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, ManufacturerID INT, MassDiff REAL, Price INT, IsStock INT, AntiSwayPhysicsID INT

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `AntiSwayPhysicsID` → `List_AntiSwayPhysics.AntiSwayPhysicsID`

### List_UpgradeAntiSwayRear

1247 rows, level 5. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, ManufacturerID INT, MassDiff REAL, Price INT, IsStock INT, AntiSwayPhysicsID INT

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `AntiSwayPhysicsID` → `List_AntiSwayPhysics.AntiSwayPhysicsID`

### List_UpgradeBrakes

1442 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, FrontBrakeSize INT, FrontBrakeTypeID INT, FrontCaliperPistons INT, RearBrakeSize INT, RearBrakeTypeID INT, RearCaliperPistons INT, GameFrictionScaleBraking REAL, BrakeTorqueSlider REAL, BrakeBiasSlider REAL, BiasHandbrake REAL, ReleasePointABS REAL, DurationABS REAL, ReleasePointABSFrontNoSteer REAL, ReleasePointABSFrontFullSteer REAL, ReleasePointABSRearNoSteer REAL, ReleasePointABSRearFullSteer REAL, DamageModDefID INT

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `FrontBrakeTypeID` → `List_BrakeType.BrakeTypeID`; `RearBrakeTypeID` → `List_BrakeType.BrakeTypeID`; `DamageModDefID` → `List_DamageModDef.DamageModDefID`

### List_UpgradeCarBody

503 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, CarBodyID INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT

Points at: `Ordinal` → `Data_Car.Id`; `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeCarBodyChassisStiffness

1266 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarbodyId INT, Level INT, ManufacturerID INT, MassDiff REAL, WeightDistDiff REAL, Price INT, PartsStringID INT, CMHeightDiff REAL, IsStock INT, FrontLatFrictionScale REAL, RearLatFrictionScale REAL, FrontLongFrictionScale REAL, RearLongFrictionScale REAL

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeCarBodyFrontBumper

1328 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarBodyID INT, Level INT, IsStock INT, Sequence INT, ManufacturerId INT, PartsStringId TEXT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, AeroPhysicsID INT

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`; `AeroPhysicsID` → `List_AeroPhysics.AeroPhysicsID`

### List_UpgradeCarBodyHood

690 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarBodyID INT, Level INT, IsStock INT, Sequence INT, ManufacturerId INT, PartsStringId TEXT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeCarBodyRearBumper

869 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarBodyID INT, Level INT, IsStock INT, Sequence INT, ManufacturerId INT, PartsStringId TEXT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, BodyAeroForwardDownforceFront REAL, BodyAeroForwardDownforceRear REAL

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeCarBodySideSkirt

820 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarBodyID INT, Level INT, IsStock INT, Sequence INT, ManufacturerId INT, PartsStringId TEXT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeCarBodyTireWidthFront

2133 rows, level 0. Key: `Id`.

Columns: Id INTEGER, CarBodyId INT, Level INT, IsStock INT, FrontTireWidth INT, Price INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `CarBodyId` → `Data_CarBody.Id` (loose: rows of 11 cut cars)

### List_UpgradeCarBodyTireWidthRear

2284 rows, level 0. Key: `Id`.

Columns: Id INTEGER, CarBodyId INT, Level INT, IsStock INT, RearTireWidth INT, Price INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `CarBodyId` → `Data_CarBody.Id` (loose: rows of 11 cut cars)

### List_UpgradeCarBodyWeight

1566 rows, level 1. Key: `Id`.

Columns: Id INTEGER, CarBodyId INT, Level INT, IsStock INT, ManufacturerID INT, Mass REAL, Price INT, PartsStringID INT, InitialMass REAL, CMHeight REAL, CMBackFront REAL, CMLeftRight REAL, BlockDimX REAL, BlockDimY REAL, BlockDimZ REAL

Points at: `CarBodyID` → `Data_CarBody.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeDrivetrain

750 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, DrivetrainID INT, PowertrainId INT, MassDiff REAL, WeightDistDiff REAL, Level INT, ManufacturerId INT, Price INT, IsStock INT

Points at: `Ordinal` → `Data_Car.Id`; `DrivetrainID` → `Data_Drivetrain.DrivetrainID`; `PowertrainId` → `Powertrains.Id`; `ManufacturerId` → `List_CarMake.ID`

### List_UpgradeDrivetrainClutch

1491 rows, level 2. Key: `Id`.

Columns: Id INTEGER, DrivetrainID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, ClutchMaxTorque REAL, ClutchInTime REAL, ClutchOutTime REAL

Points at: `DrivetrainID` → `Data_Drivetrain.DrivetrainID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeDrivetrainDifferential

1358 rows, level 2. Key: `Id`.

Columns: Id INTEGER, DrivetrainID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, FrontLimitedSlipTorqueAccel REAL, FrontLimitedSlipTorqueDecel REAL, FrontLimitedSlipRelVelClamp REAL, FrontLimitedSlipAccelDefInputTorque REAL, RearLimitedSlipTorqueAccel REAL, RearLimitedSlipTorqueDecel REAL, RearLimitedSlipRelVelClamp REAL, RearLimitedSlipAccelDefInputTorque REAL, CenterLimitedSlipTorqueAccel REAL, CenterLimitedSlipTorqueDecel REAL, CenterLimitedSlipRelVelClamp REAL, CenterLimitedSlipAccelDefInputTorque REAL, RearToqueSplit REAL

Points at: `DrivetrainID` → `Data_Drivetrain.DrivetrainID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeDrivetrainDriveline

1603 rows, level 2. Key: `Id`.

Columns: Id INTEGER, DrivetrainId INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, MomentInertia REAL

Points at: `DrivetrainID` → `Data_Drivetrain.DrivetrainID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeDrivetrainTransmission

1526 rows, level 2. Key: `Id`.

Columns: Id INTEGER, DrivetrainID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, MomentInertia REAL, GearShiftTime REAL, FinalDriveRatio REAL, NumGears INT, GearRatio0 REAL, GearRatio1 REAL, GearRatio2 REAL, GearRatio3 REAL, GearRatio4 REAL, GearRatio5 REAL, GearRatio6 REAL, GearRatio7 REAL, GearRatio8 REAL, CVTShiftTime REAL

Points at: `DrivetrainID` → `Data_Drivetrain.DrivetrainID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngine

752 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, EngineID INT, IsStock INT, ManufacturerID INT, Price INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `Ordinal` → `Data_Car.Id`; `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_CarMake.ID` (loose: the car's make; one swap uses 417)

### List_UpgradeEngineCamshaft

1418 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, RedlineRPM REAL, StallRPM REAL, StartRPM REAL, RedlineWearSpeed REAL, TorqueCurveMaxRPM REAL, NumRPMEntriesArray INT, TorqueCurveFullThrottleID INT

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `TorqueCurveFullThrottleID` → `List_TorqueCurve.TorqueCurveID`; `TorqueCurveZeroThrottleID` → `List_TorqueCurve.TorqueCurveID`

### List_UpgradeEngineCSC

442 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, ZeroRPMScale REAL, RedlineRPMScale REAL, RobScale REAL, TorqueDropOffRPM0 REAL, TorqueDropOffRPM1 REAL, TorqueDropOffScale0 REAL, TorqueDropOffScale1 REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineDisplacement

1434 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, Price INT, TorqueScale REAL, Disp INT

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineDSC

495 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, ZeroRPMScale REAL, RedlineRPMScale REAL, RobScale REAL, TorqueDropOffRPM0 REAL, TorqueDropOffRPM1 REAL, TorqueDropOffScale0 REAL, TorqueDropOffScale1 REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineExhaust

1388 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineFlywheel

1575 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerId INT, MassDiff REAL, Price INT, MomentInertia REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineFuelSystem

1338 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineIgnition

1335 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineIntake

1419 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineIntercooler

1156 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, PartsStringID INT, MassDiff REAL, WeightDistDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, MaxScaleScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`

### List_UpgradeEngineManifold

810 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineOilCooling

1382 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, WeightDistDiff REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEnginePistonsCompression

1214 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineRestrictorPlate

244 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineTurboQuad

0 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, Price INT, MomentInertia REAL, MaxScale REAL, PowerMaxScale REAL, MinScale REAL, PowerMinScale REAL, RobScale REAL, TorqueDropOffRPM0 REAL, TorqueDropOffRPM1 REAL, TorqueDropOffScale0 REAL, TorqueDropOffScale1 REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineTurboSingle

643 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, WeightDistDiff REAL, Price INT, MomentInertia REAL, MaxScale REAL, PowerMaxScale REAL, MinScale REAL, PowerMinScale REAL, RobScale REAL, TorqueDropOffRPM0 REAL, TorqueDropOffRPM1 REAL, TorqueDropOffScale0 REAL, TorqueDropOffScale1 REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineTurboTwin

512 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, WeightDistDiff REAL, Price INT, MomentInertia REAL, MaxScale REAL, PowerMaxScale REAL, MinScale REAL, PowerMinScale REAL, RobScale REAL, TorqueDropOffRPM0 REAL, TorqueDropOffRPM1 REAL, TorqueDropOffScale0 REAL, TorqueDropOffScale1 REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeEngineValves

1408 rows, level 2. Key: `Id`.

Columns: Id INTEGER, EngineID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, Price INT, TorqueScale REAL

Points at: `EngineID` → `Data_Engine.EngineID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeMotor

4 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, MotorID INT, IsStock INT, ManufacturerID INT, Price INT, MassDiff REAL, WeightDistDiff REAL

Points at: `Ordinal` → `Data_Car.Id`; `MotorID` → `Data_Motor.MotorID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeMotorParts

14 rows, level 2. Key: `Id`.

Columns: Id INTEGER, MotorID INT, Level INT, IsStock INT, ManufacturerID INT, MassDiff REAL, WeightDistDiff REAL, Price INT, TorqueScale REAL

Points at: `MotorID` → `Data_Motor.MotorID`; `ManufacturerID` → `List_PartManufacturer.Id`

### List_UpgradeRearWing

1190 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, IsStock INT, Sequence INT, ManufacturerId INT, PartsStringId TEXT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, AeroPhysicsID INT

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `PartsStringID` → `List_PartsStrings.Id`; `AeroPhysicsID` → `List_AeroPhysics.AeroPhysicsID`

### List_UpgradeRimSizeFront

1822 rows, level 0. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, IsStock INT, FrontWheelDiameter INT, Price INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `Ordinal` → `Data_Car.Id` (loose: rows of 2 cut cars)

### List_UpgradeRimSizeRear

1893 rows, level 0. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, IsStock INT, RearWheelDiameter INT, Price INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `Ordinal` → `Data_Car.Id` (loose: rows of 2 cut cars)

### List_UpgradeSpringDamper

1343 rows, level 5. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, ManufacturerID INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL, Price INT, IsStock INT, SuspensionDamageModDefID INT, TiresDamageModDefID INT, FrontSpringDamperPhysicsID INT, RearSpringDamperPhysicsID INT

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `FrontSpringDamperPhysicsID` → `List_SpringDamperPhysics.SpringDamperPhysicsID`; `RearSpringDamperPhysicsID` → `List_SpringDamperPhysics.SpringDamperPhysicsID`; `SuspensionDamageModDefID` → `List_DamageModDef.DamageModDefID`; `TiresDamageModDefID` → `List_DamageModDef.DamageModDefID`

### List_UpgradeTireCompound

2111 rows, level 4. Key: `Id`.

Columns: Id INTEGER, Ordinal INT, Level INT, IsStock INT, TireCompoundID INT, ManufacturerID INT, Price INT, MassDiff REAL, DragScale REAL, WindInstabilityScale REAL

Points at: `Ordinal` → `Data_Car.Id`; `ManufacturerID` → `List_PartManufacturer.Id`; `TireCompoundID` → `List_TireCompound.TireCompoundID`

### List_VariableTiming

3 rows, level 0. Key: `VariableTimingID`.

Columns: VariableTimingID INTEGER, VariableTimingType TEXT

Pointed at by: `Data_Engine.VariableTimingID`

### List_Wheels

583 rows, level 1. Key: `ID`.

Columns: ID INTEGER, MediaName TEXT, DisplayName TEXT, PartManufacturerID INT, Mass REAL, Price INT, IsStock INT, Type TEXT, Lip INT, CenterCap INT, Logo INT, ActualColor TEXT, WheelColor TEXT, RGB INT, BrakeDrawOffset REAL, IconPath TEXT

Points at: `PartManufacturerID` → `List_PartManufacturer.Id`

Pointed at by: `Data_Car.StockWheelID`, `WheelNormOffsets.Id`, `WheelExceptions.WheelID`

### List_WheelStyle

0 rows, level 0. Key: `WheelStyleID`.

Columns: WheelStyleID INTEGER, MediaName TEXT, DisplayName TEXT

### Livery_Categories

31 rows, level 0. Key: `ID`.

Columns: ID INTEGER, livtype INT, name TEXT, vinyllocfield TEXT, sortOrder INT

Pointed at by: `Livery_VinylsDecals.Category` (loose)

### Livery_Decals

470 rows, level 0. Key: `ID`.

Columns: ID INTEGER, Filename TEXT, DisplayName TEXT, AlphaSort TEXT

Pointed at by: `Livery_DecalsSortOrder.Livery_DecalID`

### Livery_DecalsSortOrder

3634 rows, level 3. Key: `ID`.

Columns: ID INTEGER, MakeID INT, Sequence INT, Livery_DecalID INT

Points at: `MakeID` → `List_CarMake.ID`; `Livery_DecalID` → `Livery_Decals.ID`

### Livery_VinylNames

40 rows, level 0. Key: `ID`.

Columns: ID INTEGER, SequenceText TEXT, UpperAlphaText TEXT, LowerAlphaText TEXT, LowerAlphaText2 TEXT

### Livery_VinylsDecals

880 rows, level 0. Key: `ID`.

Columns: ID INTEGER, Category INT, Name TEXT, Path TEXT

Points at: `Category` → `Livery_Categories.ID` (loose: category 20 is missing)

### MPAIDifficultyMap

4 rows, level 2. Key: `Id`.

Columns: Id INTEGER, AISkill INT, AITemperament INT

Points at: `AISkill` → `AISkills.id`; `AITemperament` → `AITemperaments.id`

### MultiDiscContentPacks

5 rows, level 0. Key: `ID (first column, not declared)`.

Columns: ID TEXT, DashDisplayName TEXT, Description TEXT, LongDescription TEXT, intID INT, PubOfferingID INT, LicenseLevel INT, LicenseMask INT, IsDLC INT

### MultiDiscSlides

3 rows, level 0. Key: none.

Columns: contentPackID TEXT, slideNumber INT, imagePath TEXT

### NewProfile_BadgeTitleProgress

0 rows, level 0. Key: `RuleId`.

Columns: RuleId INTEGER, CurrentProgress INT

### NewProfile_BadgeTitleUnlocks

0 rows, level 0. Key: `UnlockId`.

Columns: UnlockId INTEGER, IsNewBadge INT, IsNewTitle INT

### NewProfile_Career_Affinity

0 rows, level 0. Key: `GroupId`.

Columns: GroupId INTEGER, XP INT, TimeDriven INT, DistanceDriven INT

### NewProfile_Career_AIHistory

0 rows, level 0. Key: `AIPlayerId`.

Columns: AIPlayerId INTEGER, Lvl INT, TotalWinnings INT, DistanceDriven INT, TimeDriven INT, NumVictories INT, NumPodiums INT, NumRaces INT, TotalRepairs INT

### NewProfile_Career_CarGroups

0 rows, level 0. Key: `ID`.

Columns: ID INTEGER, SortOrder INT, DisplayName TEXT

### NewProfile_Career_EventResults

0 rows, level 0. Key: `EventId`.

Columns: EventId INTEGER, Place INT

Points at: `EventId` → `Events.Id` (loose: profile table)

### NewProfile_Career_Garage

0 rows, level 0. Key: `Id`.

Columns: Id INTEGER, CarId INT, PerformanceIndex REAL, ClassID INT, PartsValue INT, SpeedRating REAL, AccelerationRating REAL, LaunchRating REAL, BrakingRating REAL, HandlingRating REAL, CurbWeight REAL, WeightDistribution REAL, AspirationTypeId INT, SimPeakPower REAL, SimPeakAngVel REAL, SimPeakTorque REAL, SimPeakTorqueAngVel REAL, SimRedlineAngVel REAL, PeakIntakePSI REAL, DistanceDriven INT, TimeDriven INT, TotalWinnings INT, TotalRepairs INT, NumVictories INT, NumPodiums INT, NumRaces INT, NumOwners INT, NumTimesSold INT, CurOwnerPurchaseDate INT, CurOwnerNumRaces INT, CurOwnerWinnings INT, OriginalOwner TEXT, CarGroup INT, Flags INT, Engine INT, Drivetrain INT, CarBody INT, Motor INT, Brakes INT, SpringDamper INT, AntiSwayFront INT, AntiSwayRear INT, TireCompound INT, RearWing INT, RimSizeFront INT, RimSizeRear INT, Camshaft INT, Valves INT, Displacement INT, PistonsCompression INT, FuelSystem INT, Ignition INT, Exhaust INT, Intake INT, Flywheel INT, Manifold INT, RestrictorPlate INT, OilCooling INT, SingleTurbo INT, TwinTurbo INT, QuadTurbo INT, SuperchargerCSC INT, SuperchargerDSC INT, Intercooler INT, Clutch INT, Transmission INT, Driveline INT, Differential INT, FrontBumper INT, RearBumper INT, Hood INT, SideSkirts INT, TireWidthFront INT, TireWidthRear INT, WeightReduction INT, ChassisStiffness INT, MotorParts INT, TireBrand INT, WheelStyle INT, DefaultManufacturerColorIndex INT, LiveryFileName TEXT, Guid TEXT, Thumbnail TEXT, Tuning_frontTirePressure REAL, Tuning_rearTirePressure REAL, Tuning_finalDriveRatio REAL, Tuning_firstGear REAL, Tuning_secondGear REAL, Tuning_thirdGear REAL, Tuning_fourthGear REAL, Tuning_fifthGear REAL, Tuning_sixthGear REAL, Tuning_seventhGear REAL, Tuning_eighthGear REAL, Tuning_frontCamber REAL, Tuning_rearCamber REAL, Tuning_frontToe REAL, Tuning_rearToe REAL, Tuning_frontCaster REAL, Tuning_frontSwaybar REAL, Tuning_rearSwaybar REAL, Tuning_frontSpring REAL, Tuning_rearSpring REAL, Tuning_frontRideHeight REAL, Tuning_rearRideHeight REAL, Tuning_frontDampingStiffness REAL, Tuning_rearDampingStiffness REAL, Tuning_frontBumpRatio REAL, Tuning_rearBumpRatio REAL, Tuning_frontDownforce REAL, Tuning_rearDownforce REAL, Tuning_brakeBalance REAL, Tuning_brakePressure REAL, Tuning_frontAccel REAL, Tuning_rearAccel REAL, Tuning_frontDecel REAL, Tuning_rearDecel REAL, Tuning_centerTorque REAL, SharedID INT

Points at: `CarId` → `Data_Car.Id` (loose: profile table, empty on the disc)

### NewProfile_Career_PurchasedParts

0 rows, level 2. Key: none.

Columns: GarageId INT, PartEnum INT, PartId INT, ManufacturerId INT, PricePaid INT

Points at: `PartEnum` → `UpgradeTypes.PartEnum`

### NewProfile_Career_RaceResults

0 rows, level 0. Key: `RaceId`.

Columns: RaceId INTEGER, Place INT

Points at: `RaceId` → `Races.Id` (loose: profile table)

### NewProfile_Career_TrackUsage

0 rows, level 0. Key: `TrackId`.

Columns: TrackId INTEGER, NumCareerRaces INT, NumNonCareerRaces INT, Viewed INT

Points at: `TrackId` → `Tracks.id` (loose: profile table)

### NewProfile_CarExperienceRevealsSeen

0 rows, level 0. Key: `CarID`.

Columns: CarID INTEGER

Points at: `CarID` → `Data_Car.Id` (loose: profile table)

### NewProfile_CarExperienceUnlocks

4 rows, level 0. Key: `CarID`.

Columns: CarID INTEGER

Points at: `CarID` → `Data_Car.Id` (loose: profile table)

### NewProfile_CarPointsOfInterestProgress

0 rows, level 0. Key: `CarOrdinal`.

Columns: CarOrdinal INTEGER, NumPOIActivations INT

Points at: `CarOrdinal` → `Data_Car.Id` (loose: profile table)

### NewProfile_ChangeTracking

0 rows, level 0. Key: none.

Columns: TableID INT, RowID INT, Action INT, TransactionId INT

### NewProfile_ContentSeen

0 rows, level 0. Key: `ContentType_ContentId`.

Columns: ContentType_ContentId INTEGER

### NewProfile_GameplayAggregate

0 rows, level 0. Key: `EntryType`.

Columns: EntryType INTEGER, Count INT, Value INT

### NewProfile_LastUsedGarageId

1 rows, level 0. Key: `LastUsedGarageId`.

Columns: LastUsedGarageId INTEGER

### PerformanceBudget

261 rows, level 1. Key: none.

Columns: BudgetArea TEXT, BudgetItem TEXT, MetricName TEXT, Scenario TEXT, Budget REAL, Units TEXT, LongDescription TEXT, CameraType INT, DisplayOrder INT

Points at: `CameraType` → `PerformanceBudgetCameras.CameraType`

### PerformanceBudgetCameras

5 rows, level 0. Key: `CameraType`.

Columns: CameraName TEXT, CameraType INTEGER

Pointed at by: `PerformanceBudget.CameraType`

### PittingProfiles

18 rows, level 0. Key: `id`.

Columns: id INTEGER, Name TEXT, PitThreshold_Tires REAL, PitThreshold_Fuel REAL, PitThreshold_Engine REAL, PitThreshold_Transmission REAL, PitThreshold_Suspension REAL, PitThreshold_Deviation REAL

Pointed at by: `Tracks.MPPitProfile` (loose), `Events.PittingProfileID`

### PlayerLevelDef

151 rows, level 0. Key: `Id`.

Columns: Id INTEGER, XP INT, MaxGiftCars INT

Pointed at by: `Events.Level`, `Rewards_DriverLevelv3.DriverLevel`

### Powertrains

8 rows, level 1. Key: `Id`.

Columns: Id INTEGER, DrivetrainId INT, EnginePlacementId INT, IconPath TEXT, SmallIconPath TEXT

Points at: `DrivetrainId` → `List_DriveType.ID`; `EnginePlacementId` → `List_EnginePlacement.ID` (loose: row 7 uses 4)

Pointed at by: `Data_Car.PowertrainID`, `List_UpgradeDrivetrain.PowertrainId`

### Races

1204 rows, level 5. Key: `Id`.

Columns: Id INTEGER, EventId INT, RaceNumber INT, TrackId INT, NumLaps INT, ModeRaceNumber INT, TimeOfDayIndex INT

Points at: `TrackId` → `Tracks.id`; `EventId` → `Events.Id`; `RaceModeId` → `RaceModes.id`

Pointed at by: `RacesSpecial.RaceId`, `NewProfile_Career_RaceResults.RaceId` (loose)

### RacesSpecial

86 rows, level 6. Key: `RaceId`.

Columns: RaceId INTEGER, CollObjLayoutId INT, TargetScore_Easy REAL, TargetScore_Med REAL, TargetScore_Hard REAL, TargetScore_Pro REAL, MinSpeedRequiredMPH REAL

Points at: `RaceId` → `Races.Id`

### Rewards_DriverLevelv3

203 rows, level 4. Key: `Id`.

Columns: Id INTEGER, DriverLevel INT, RewardCarId INT, SortOrder INT

Points at: `RewardCarId` → `Data_Car.Id`; `DriverLevel` → `PlayerLevelDef.Id`; `InitialCar` → `Data_Car.Id`

### Rewards_ShowroomUnlocks

2 rows, level 4. Key: `RewardCarId`.

Columns: NumCompletedUnlocks INT, RewardCarId INTEGER

Points at: `RewardCarId` → `Data_Car.Id`

### SplitPoints

432 rows, level 0. Key: `ID`.

Columns: ID INTEGER, TrackId INT, Split REAL

Points at: `TrackId` → `Tracks.id` (loose: cut layouts)

### StartGridPositions

1073 rows, level 0. Key: `id (first column, not declared)`.

Columns: id INT, StartIndex INT, MetersBackFromStartLine REAL, MetersRightOfCenterLine REAL, Yaw REAL

Pointed at by: `Tracks.DefaultStartGridPositionsId`

### StripeColors

243 rows, level 0. Key: none.

Columns: Ordinal INT, Sequence INT, ColorStripe INT, MetallicAmount REAL

Points at: `Ordinal` → `Data_Car.Id` (loose: rows of cut cars); `Ordinal,Sequence` → `Combo_Colors.Ordinal,Sequence` (loose: rows of cut cars)

### TeamColors

4 rows, level 0. Key: `id`.

Columns: id INTEGER, rgb INT, DisplayName TEXT

### ThumbnailTypes

6 rows, level 0. Key: `Id (first column, not declared)`.

Columns: Id INT, TableName TEXT, ColumnName TEXT

### TireMod_FrictionScale

10 rows, level 0. Key: `Ordinal`.

Columns: Ordinal INTEGER, FrontFrictionScale REAL, RearFrictionScale REAL

Points at: `Ordinal` → `Data_Car.Id` (loose: one cut car)

### TrackPittingZones

80 rows, level 0. Key: `Tracks_id`.

Columns: Tracks_id INTEGER, SpeedLimit REAL, EnterFirstMergeWP INT, EnterLastMergeWP INT, EnterMergeCompleteWP INT, ExitMergeWP INT, ExitMergeCompleteWP INT

Points at: `Tracks_id` → `Tracks.id` (loose: cut layouts)

### Tracks

127 rows, level 3. Key: `id`.

Columns: id INTEGER, DisplayName TEXT, ShortDisplayName TEXT, MediaName TEXT, RibbonIndex INT, ReverseIndex INT, Length INT, DefaultStartGridPositionsId INT, FlyingStartPosId INT, Logo TEXT, LogoSmall TEXT, IsRealWorld INT, IsReverse INT, IsHomespace INT, Active INT, MiniRibbonPath TEXT, ContentId INT, IsPurchased INT, MPSortOrder INT, MPPitProfile INT, RibbonConfiguration INT, CarsPerUniverse INT, Renderable INT, DriftScoreScalar REAL, StartLine REAL, FinishLine REAL, XMirror321 INT, IsInstalled INT, EnvironmentId INT, WinningsScalar REAL, QuickRaceLapCountKinect INT, QuickRaceLapCount INT, RichPresence INT, SatImageName TEXT

Points at: `EnvironmentId` → `Environments.Id`; `DefaultStartGridPositionsId` → `StartGridPositions.id`; `FlyingStartPosId` → `FlyingStartPositions.id` (loose: 16 tracks point at missing rows); `MPPitProfile` → `PittingProfiles.id` (loose: one track uses 200)

Pointed at by: `Races.TrackId`, `HopperRaces.TrackId`, `KinectSplitscreenTracks.TrackId` (loose), `SplitPoints.TrackId` (loose), `GraffitiPoints.TrackId` (loose), `TurnSections.TrackId` (loose), `TrackPittingZones.Tracks_id` (loose), `TrackStartingMerges.Tracks_id` (loose), `TrackSpecificAI.Tracks_id` (loose), `NewProfile_Career_TrackUsage.TrackId` (loose), `Anthem360OnlineEvents.TrackId`

### TrackSpecificAI

12 rows, level 0. Key: `ID`.

Columns: ID INTEGER, Tracks_id INT, AIDiff_id INT, DistBehindForTorqueBoost REAL, StartRubberbandingDistance REAL, MaxRubberbandingEffectDistance REAL, StartTorqueCutDistance REAL, MaxTorqueCutDistance REAL

Points at: `Tracks_id` → `Tracks.id` (loose); `AIDiff_id` → `AIDiff.id` (loose: uses 3)

### TrackStartingMerges

157 rows, level 0. Key: `Tracks_id`.

Columns: Tracks_id INTEGER, WaypointsToFirstMerge INT, WaypointsBeforeCorner INT, WaypointsBetweenMerges INT, MaxStartOfflineDistance REAL

Points at: `Tracks_id` → `Tracks.id` (loose: cut layouts)

### TrialLoadingImages

7 rows, level 0. Key: `ID (first column, not declared)`.

Columns: ID INT, imagePath TEXT

### TurnSections

1095 rows, level 0. Key: none.

Columns: TrackId INT, Begin REAL, End REAL, Apex REAL

Points at: `TrackId` → `Tracks.id` (loose: cut layouts)

### UpgradeAreas

7 rows, level 0. Key: `id`.

Columns: id INTEGER, Name TEXT, Description TEXT, IconPath TEXT, ImagePath TEXT

Pointed at by: `UpgradeTypes.AreaId`

### Upgrades

204 rows, level 2. Key: `id`.

Columns: id INTEGER, TypeId TEXT, IsException INT, Name TEXT, Level INT, Description TEXT, IconPath TEXT, ImagePath TEXT

Points at: `TypeId` → `UpgradeTypes.id`

### UpgradeTypes

46 rows, level 1. Key: `id`.

Columns: id INTEGER, PartEnum INT, AreaId INT, IsException INT, DisplayOrder INT, Name TEXT, Description TEXT, IconPath TEXT, ImagePath TEXT

Points at: `AreaId` → `UpgradeAreas.id`

Pointed at by: `Upgrades.TypeId`, `UpgradeWizardParts.PartEnum`, `UpgradeWizardPartsRemoval.PartEnum`, `UpgradeWizardPowerRemoval.PartEnum`, `NewProfile_Career_PurchasedParts.PartEnum`, `List_Aspiration.KeyPartEnum` (loose), `CarInvalidDefaultParts.PartEnumValue`, `Rewards_CarPartDiscounts.PartEnumId`, `OptionalRallyUpgrades.PartEnum`

### UpgradeWizardParts

83 rows, level 2. Key: none.

Columns: PartEnum INT, PartLevel INT, SortBalanced INT

Points at: `PartEnum` → `UpgradeTypes.PartEnum`

### UpgradeWizardPartsRemoval

30 rows, level 2. Key: `PartEnum`.

Columns: PartEnum INTEGER, SortBalanced INT

Points at: `PartEnum` → `UpgradeTypes.PartEnum`

### UpgradeWizardPowerRemoval

17 rows, level 2. Key: `PartEnum`.

Columns: PartEnum INTEGER, RemoveOrder INT

Points at: `PartEnum` → `UpgradeTypes.PartEnum`

### VersionInfo

1 rows, level 0. Key: `database_version`.

Columns: database_version INTEGER

### VolatileHostOptions

11 rows, level 1. Key: none.

Columns: OptionId INT

Points at: `OptionId` → `GameOptions.Id`

### WheelExceptions

30 rows, level 2. Key: `WheelID`.

Columns: WheelID INTEGER

Points at: `WheelID` → `List_Wheels.ID`

### WheelLayouts

5 rows, level 0. Key: `LayoutId`.

Columns: LayoutId INTEGER, LeftThumbUp TEXT, LeftThumbDown TEXT, LeftThumbButton TEXT, RightThumbUp TEXT, RightThumbDown TEXT, RightThumbLeft TEXT, RightThumbRight TEXT, RightThumbButton TEXT, DPadUp TEXT, DPadDown TEXT, DPadLeft TEXT, DPadRight TEXT, ButtonA TEXT, ButtonB TEXT, ButtonX TEXT, ButtonY TEXT, LeftShoulder TEXT, RightShoulder TEXT, LeftTrigger TEXT, RightTrigger TEXT

### WheelNormOffsets

111 rows, level 2. Key: `Id (first column, not declared)`.

Columns: Id INT, NormOffset REAL

Points at: `Id` → `List_Wheels.ID`

