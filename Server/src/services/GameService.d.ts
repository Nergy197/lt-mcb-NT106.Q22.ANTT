import { Types } from 'mongoose';
import { IPokemonInstance } from '../models/PokemonInstance';
export declare class GameService {
    /**
     * 1. Heal at Safe Zone (Hub)
     * Finds all PokemonInstances in a player's party and restores their HP and status.
     */
    static healPlayerParty(playerId: string | Types.ObjectId): Promise<boolean>;
    /**
     * 2. Catch Wild Pokemon
     * Generates a new PokemonInstance with random IVs and Nature for the player.
     */
    static catchPokemon(playerId: string | Types.ObjectId, speciesId: number, baseHp: number): Promise<IPokemonInstance>;
    /**
     * 3. Secure Trading (Hub)
     * Uses a MongoDB Transaction to swap the owner_id of two Pokemon Instances simultaneously.
     */
    static executeTrade(player1Id: string | Types.ObjectId, player2Id: string | Types.ObjectId, pokemonInstanceId1: string | Types.ObjectId, pokemonInstanceId2: string | Types.ObjectId): Promise<boolean>;
    /**
     * 4. Boss Gating System (Wilderness)
     * Returns true if requiredBossId is in the player's beaten_bosses array.
     */
    static checkCanEnterZone(playerId: string | Types.ObjectId, requiredBossId: string): Promise<boolean>;
    /**
     * 5. Defeat Boss
     * Pushes the bossId into the player's beaten_bosses array.
     */
    static onBossDefeated(playerId: string | Types.ObjectId, bossId: string): Promise<void>;
}
//# sourceMappingURL=GameService.d.ts.map