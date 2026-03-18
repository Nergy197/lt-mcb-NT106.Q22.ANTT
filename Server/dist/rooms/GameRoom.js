"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.GameRoom = void 0;
const core_1 = require("@colyseus/core");
const GameState_1 = require("./GameState");
const GameService_1 = require("../services/GameService");
const Player_1 = require("../models/Player");
const PokemonInstance_1 = require("../models/PokemonInstance");
class GameRoom extends core_1.Room {
    maxClients = 100;
    onCreate(options) {
        this.setState(new GameState_1.GameState());
        this.onMessage("move", (client, data) => {
            const player = this.state.players.get(client.sessionId);
            if (player) {
                player.x = data.x;
                player.y = data.y;
                player.z = data.z;
            }
        });
        this.onMessage("heal", async (client) => {
            const player = this.state.players.get(client.sessionId);
            if (player) {
                try {
                    await GameService_1.GameService.healPlayerParty(player.id);
                    await this.syncPartyToState(client.sessionId, player.id);
                }
                catch (e) {
                    client.send("error", { message: "Failed to heal party" });
                }
            }
        });
        this.onMessage("catch", async (client, data) => {
            const playerInState = this.state.players.get(client.sessionId);
            if (playerInState) {
                try {
                    const newPoke = await GameService_1.GameService.catchPokemon(playerInState.id, data.speciesId);
                    const statePoke = new GameState_1.PokemonInState();
                    statePoke.id = newPoke._id.toString();
                    statePoke.species_id = newPoke.species_id;
                    statePoke.level = newPoke.level;
                    statePoke.hp = newPoke.current_hp;
                    statePoke.maxHp = newPoke.max_hp;
                    playerInState.party.set(statePoke.id, statePoke);
                }
                catch (e) {
                    client.send("error", { message: "Catch failed" });
                }
            }
        });
    }
    async onJoin(client, options) {
        console.log(`[Join] Player joining with ID: ${options.playerId}`);
        const playerRecord = await Player_1.PlayerModel.findById(options.playerId);
        if (!playerRecord) {
            client.leave();
            return;
        }
        const playerInState = new GameState_1.PlayerInState();
        playerInState.id = playerRecord._id.toString();
        playerInState.name = playerRecord.name;
        playerInState.x = playerRecord.position.x;
        playerInState.y = playerRecord.position.y;
        playerInState.z = playerRecord.position.z;
        this.state.players.set(client.sessionId, playerInState);
        await this.syncPartyToState(client.sessionId, playerInState.id);
    }
    onLeave(client, code) {
        console.log(`[Leave] Client ${client.sessionId} left (code: ${code}).`);
        this.state.players.delete(client.sessionId);
    }
    async syncPartyToState(sessionId, playerId) {
        const playerInState = this.state.players.get(sessionId);
        if (!playerInState)
            return;
        const partyMembers = await PokemonInstance_1.PokemonInstanceModel.find({ owner_id: playerId, is_in_party: true });
        playerInState.party.clear();
        partyMembers.forEach((poke) => {
            const p = new GameState_1.PokemonInState();
            p.id = poke._id.toString();
            p.species_id = poke.species_id;
            p.level = poke.level;
            p.hp = poke.current_hp;
            p.maxHp = poke.max_hp;
            playerInState.party.set(p.id, p);
        });
    }
}
exports.GameRoom = GameRoom;
