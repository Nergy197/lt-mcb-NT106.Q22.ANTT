import { Room, Client } from '@colyseus/core';
import { GameState, PlayerInState, PokemonInState } from './GameState';
import { GameService } from '../services/GameService';
import { PlayerModel } from '../models/Player';
import { PokemonInstanceModel } from '../models/PokemonInstance';

export class GameRoom extends Room<{ state: GameState }> {
  maxClients = 100;

  onCreate(options: any) {
    this.setState(new GameState());

    this.onMessage("move", (client: Client, data: { x: number, y: number, z: number }) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.x = data.x;
        player.y = data.y;
        player.z = data.z;
      }
    });

    this.onMessage("heal", async (client: Client) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        try {
          await GameService.healPlayerParty(player.id);
          await this.syncPartyToState(client.sessionId, player.id);
        } catch (e) {
          client.send("error", { message: "Failed to heal party" });
        }
      }
    });

    this.onMessage("catch", async (client: Client, data: { speciesId: number }) => {
      const playerInState = this.state.players.get(client.sessionId);
      if (playerInState) {
        try {
          const newPoke = await GameService.catchPokemon(playerInState.id, data.speciesId);
          const statePoke = new PokemonInState();
          statePoke.id = newPoke._id.toString();
          statePoke.species_id = newPoke.species_id;
          statePoke.level = newPoke.level;
          statePoke.hp = newPoke.current_hp;
          statePoke.maxHp = newPoke.max_hp;
          playerInState.party.set(statePoke.id, statePoke);
        } catch (e) {
          client.send("error", { message: "Catch failed" });
        }
      }
    });
  }

  async onJoin(client: Client, options: { playerId: string }) {
    console.log(`[Join] Player joining with ID: ${options.playerId}`);
    const playerRecord = await PlayerModel.findById(options.playerId);
    if (!playerRecord) {
      client.leave();
      return;
    }

    const playerInState = new PlayerInState();
    playerInState.id = playerRecord._id.toString();
    playerInState.name = playerRecord.name;
    playerInState.x = playerRecord.position.x;
    playerInState.y = playerRecord.position.y;
    playerInState.z = playerRecord.position.z;

    this.state.players.set(client.sessionId, playerInState);
    await this.syncPartyToState(client.sessionId, playerInState.id);
  }

  onLeave(client: Client, code?: number) {
    console.log(`[Leave] Client ${client.sessionId} left (code: ${code}).`);
    this.state.players.delete(client.sessionId);
  }

  private async syncPartyToState(sessionId: string, playerId: string) {
    const playerInState = this.state.players.get(sessionId);
    if (!playerInState) return;

    const partyMembers = await PokemonInstanceModel.find({ owner_id: playerId, is_in_party: true });
    playerInState.party.clear();
    partyMembers.forEach((poke) => {
      const p = new PokemonInState();
      p.id = poke._id.toString();
      p.species_id = poke.species_id;
      p.level = poke.level;
      p.hp = poke.current_hp;
      p.maxHp = poke.max_hp;
      playerInState.party.set(p.id, p);
    });
  }
}
