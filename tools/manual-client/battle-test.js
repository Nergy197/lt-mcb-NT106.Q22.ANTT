const signalR = require("@microsoft/signalr");

const BASE_URL = process.env.BASE_URL || "http://localhost:2567";
const USER_A = process.env.USER_A || "manual_player_a";
const USER_B = process.env.USER_B || "manual_player_b";
const PASS_A = process.env.PASS_A || "123456";
const PASS_B = process.env.PASS_B || "123456";

const mode = process.argv[2] || "normal";
let sharedBattleId = null;

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function login(username, password) {
  const response = await fetch(`${BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  const data = await response.json();
  if (!response.ok) {
    throw new Error(`Login failed for ${username}: ${data.message || response.status}`);
  }

  if (!data.playerId) {
    throw new Error(
      `Login succeeded for ${username} but no playerId in response. Seed players first.`
    );
  }

  return data;
}

function wireEvents(name, connection) {
  connection.on("Error", (payload) => console.log(`[${name}] Error`, payload));
  connection.on("PlayerJoined", (payload) => console.log(`[${name}] PlayerJoined`, payload));
  connection.on("PlayerLeft", (payload) => console.log(`[${name}] PlayerLeft`, payload));
  connection.on("PartyUpdated", (payload) => console.log(`[${name}] PartyUpdated`, payload));
  connection.on("ActionAccepted", (payload) => console.log(`[${name}] ActionAccepted`, payload));
  connection.on("TurnWaiting", (payload) => console.log(`[${name}] TurnWaiting`, payload));
  connection.on("TurnResolved", (payload) => console.log(`[${name}] TurnResolved`, payload));
  connection.on("BattleUpdated", (payload) => console.log(`[${name}] BattleUpdated`, payload));
  connection.on("BattleEnded", (payload) => console.log(`[${name}] BattleEnded`, payload));
  connection.on("BattleStarted", (payload) => {
    console.log(`[${name}] BattleStarted`, payload);
    if (!sharedBattleId) sharedBattleId = payload.battleId;
  });
}

async function main() {
  console.log("Logging in test users...");
  const authA = await login(USER_A, PASS_A);
  const authB = await login(USER_B, PASS_B);
  console.log(`A playerId=${authA.playerId}`);
  console.log(`B playerId=${authB.playerId}`);

  const connA = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE_URL}/game`, { accessTokenFactory: () => authA.token })
    .withAutomaticReconnect()
    .build();

  const connB = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE_URL}/game`, { accessTokenFactory: () => authB.token })
    .withAutomaticReconnect()
    .build();

  wireEvents("A", connA);
  wireEvents("B", connB);

  await connA.start();
  await connB.start();

  await connA.invoke("JoinGame");
  await connB.invoke("JoinGame");
  await sleep(400);

  await connA.invoke("StartMatch", authB.playerId);

  for (let i = 0; i < 50 && !sharedBattleId; i++) {
    await sleep(200);
  }
  if (!sharedBattleId) throw new Error("Did not receive BattleStarted.");

  if (mode === "timeout") {
    console.log("Timeout mode: only A submits action, then trigger SyncBattle.");
    await connA.invoke("ChooseMove", sharedBattleId, 0);
    await sleep(32000);
    await connA.invoke("SyncBattle", sharedBattleId);
  } else if (mode === "switch") {
    console.log("Switch mode: A attacks, B tries switch index 0 then move.");
    await connA.invoke("ChooseMove", sharedBattleId, 0);
    await connB.invoke("SwitchPokemon", sharedBattleId, 0);
    await sleep(1500);
    await connB.invoke("ChooseMove", sharedBattleId, 0);
  } else {
    console.log("Normal mode: both sides choose move 0.");
    await connA.invoke("ChooseMove", sharedBattleId, 0);
    await connB.invoke("ChooseMove", sharedBattleId, 0);
  }

  await sleep(4000);
  await connA.stop();
  await connB.stop();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
