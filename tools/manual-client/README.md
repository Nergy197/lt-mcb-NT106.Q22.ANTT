# Manual Battle Client

Quick scripts to test SignalR game logic without Unity UI.

## 1) Install dependencies

```bash
npm install
```

## 2) Seed test data

```bash
npm run seed
```

Creates/upserts:
- 2 accounts (`manual_player_a`, `manual_player_b`, password `123456`)
- 2 players linked to those accounts
- basic moves/pokedex records
- 1 in-party Pokemon per player

## 3) Run battle scenarios

```bash
npm run battle
```

Other scenarios:

```bash
npm run battle:timeout
npm run battle:switch
```

Environment overrides:
- `BASE_URL` (default `http://localhost:2567`)
- `USER_A`, `PASS_A`
- `USER_B`, `PASS_B`
