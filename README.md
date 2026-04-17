# SkillGainModifier

A Valheim mod which allows modifying experience gain per-skill

- Global value for all skills
- Per-skill (override with any value other than 0)

You can also modify the skill reduction modifier which is applied when dying, and set the corpse run duration. Currently this value is baked into the tombstone when dying,  meaning that changing the value will only affect the tombstones generated after

Disabling the no skill drain effect is possible. Currently this doesn't remove the UI icon though, but functionally it works!

## Config hot reloading

Supports hot reloading the config while in-game. This can be done by modifying the config (via a mod manager or by just editing it) and saving it. The mod watches for any changes made on the file while in-game and reloads it if necessary
