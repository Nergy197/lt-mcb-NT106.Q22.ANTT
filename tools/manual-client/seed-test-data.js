const bcrypt = require("bcryptjs");
const { MongoClient, ObjectId } = require("mongodb");

const MONGO_URI = process.env.MONGO_URI || "mongodb://localhost:27017";
const MONGO_DB = process.env.MONGO_DB || "pokemon_mmo";

const USERS = [
  {
    username: "manual_player_a",
    email: "manual_player_a@example.com",
    password: "123456",
    playerName: "ManualPlayerA",
    speciesId: 4,
    moveId: 9001,
    nickname: "BlazeA",
  },
  {
    username: "manual_player_b",
    email: "manual_player_b@example.com",
    password: "123456",
    playerName: "ManualPlayerB",
    speciesId: 7,
    moveId: 9002,
    nickname: "WaveB",
  },
];

const MOVE_DATA = [
  { id: 9001, name: "Seed Flamethrower", type: "fire", power: 90, accuracy: 100, priority: 0, category: "Special", pp: 15 },
  { id: 9002, name: "Seed Hydro Pump", type: "water", power: 90, accuracy: 100, priority: 0, category: "Special", pp: 10 },
];

const POKEDEX_DATA = [
  {
    id: 4,
    name: "charmander",
    types: ["fire"],
    base_stats: { hp: 39, attack: 52, defense: 43, "special-attack": 60, "special-defense": 50, speed: 65 },
    sprite_url: "",
    height: 6,
    weight: 85,
  },
  {
    id: 7,
    name: "squirtle",
    types: ["water"],
    base_stats: { hp: 44, attack: 48, defense: 65, "special-attack": 50, "special-defense": 64, speed: 43 },
    sprite_url: "",
    height: 5,
    weight: 90,
  },
];

function createPokemon(ownerId, user) {
  return {
    owner_id: ownerId,
    species_id: user.speciesId,
    nickname: user.nickname,
    level: 50,
    exp: 0,
    nature: "Hardy",
    current_hp: 180,
    max_hp: 180,
    status_condition: "NONE",
    is_in_party: true,
    party_slot: 0,
    is_trial: false,
    ivs: { hp: 31, atk: 31, def: 31, spatk: 31, spdef: 31, spd: 31 },
    evs: { hp: 0, atk: 0, def: 0, spatk: 0, spdef: 0, spd: 0 },
    moves: [{ move_id: user.moveId, current_pp: 15 }],
  };
}

async function upsertMoveAndDex(db) {
  const moves = db.collection("moves");
  const pokedex = db.collection("pokedex");

  for (const move of MOVE_DATA) {
    await moves.updateOne(
      { id: move.id },
      {
        $set: {
          name: move.name,
          power: move.power,
          accuracy: move.accuracy,
          type: move.type,
          priority: move.priority,
          category: move.category,
          pp: move.pp,
        },
      },
      { upsert: true }
    );
  }

  for (const dex of POKEDEX_DATA) {
    await pokedex.updateOne(
      { id: dex.id },
      { $set: dex },
      { upsert: true }
    );
  }
}

async function upsertUserData(db, user) {
  const accounts = db.collection("accounts");
  const players = db.collection("players");
  const pokemonInstances = db.collection("pokemoninstances");

  const passwordHash = await bcrypt.hash(user.password, 10);
  await accounts.updateOne(
    { username: user.username },
    {
      $set: {
        email: user.email,
        password_hash: passwordHash,
      },
      $setOnInsert: {
        created_at: new Date(),
      },
      $unset: {
        password_reset_token: "",
        password_reset_expiry: "",
      },
    },
    { upsert: true }
  );

  const account = await accounts.findOne({ username: user.username });
  if (!account) throw new Error(`Cannot load account for ${user.username}`);

  const accountId = account._id instanceof ObjectId ? account._id : new ObjectId(account._id);
  await players.updateOne(
    { account_id: accountId },
    {
      $set: {
        name: user.playerName,
      },
      $setOnInsert: {
        vp: 0,
        mmr: 1000,
        ranked_wins: 0,
        ranked_matches: 0,
      },
    },
    { upsert: true }
  );

  const player = await players.findOne({ account_id: accountId });
  if (!player) throw new Error(`Cannot load player for ${user.username}`);

  const playerId = player._id instanceof ObjectId ? player._id : new ObjectId(player._id);

  await pokemonInstances.deleteMany({ owner_id: playerId });
  await pokemonInstances.insertOne(createPokemon(playerId, user));

  return {
    username: user.username,
    password: user.password,
    playerId: playerId.toString(),
    accountId: accountId.toString(),
  };
}

async function main() {
  const client = new MongoClient(MONGO_URI);
  await client.connect();

  try {
    const db = client.db(MONGO_DB);
    await upsertMoveAndDex(db);

    const seeded = [];
    for (const user of USERS) {
      seeded.push(await upsertUserData(db, user));
    }

    console.log("Seed completed.");
    for (const item of seeded) {
      console.log(
        `- username=${item.username} password=${item.password} playerId=${item.playerId} accountId=${item.accountId}`
      );
    }
  } finally {
    await client.close();
  }
}

main().catch((err) => {
  console.error("Seed failed:", err);
  process.exit(1);
});
