"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.GameService = void 0;
const mongoose_1 = __importDefault(require("mongoose"));
const Player_1 = require("../models/Player");
const PokemonInstance_1 = require("../models/PokemonInstance");
const PokemonStats_1 = require("../models/PokemonStats");
const POKEMON_NATURES = [
    'Hardy', 'Lonely', 'Brave', 'Adamant', 'Naughty',
    'Bold', 'Docile', 'Relaxed', 'Impish', 'Lax',
    'Timid', 'Hasty', 'Serious', 'Jolly', 'Naive',
    'Modest', 'Mild', 'Quiet', 'Bashful', 'Rash',
    'Calm', 'Gentle', 'Sassy', 'Careful', 'Quirky'
];
class GameService {
    /**
     * 1. Heal at Safe Zone (Hub)
     * Finds all PokemonInstances in a player's party and restores their HP and status.
     */
    static async healPlayerParty(playerId) {
        try {
            // Find all pokemon in the player's party
            const party = await PokemonInstance_1.PokemonInstanceModel.find({
                owner_id: playerId,
                is_in_party: true
            });
            if (party.length === 0)
                return true; // Nothing to heal
            // Update each Pokemon to full health and remove status conditions.
            const bulkOps = party.map(pokemon => ({
                updateOne: {
                    filter: { _id: pokemon._id },
                    update: {
                        $set: {
                            current_hp: pokemon.max_hp,
                            status_condition: 'NONE'
                        }
                    }
                }
            }));
            await PokemonInstance_1.PokemonInstanceModel.bulkWrite(bulkOps);
            return true;
        }
        catch (error) {
            console.error('Error healing player party:', error);
            throw new Error('Failed to heal party');
        }
    }
    /**
     * 2. Catch Wild Pokemon
     * Generates a new PokemonInstance with random IVs and Nature for the player.
     */
    static async catchPokemon(playerId, speciesId, baseHp) {
        const session = await mongoose_1.default.startSession();
        session.startTransaction();
        try {
            // Step 1: Generate IVs (0-31) and randomly select a Nature
            const generateIV = () => Math.floor(Math.random() * 32);
            const ivs = {
                hp: generateIV(),
                atk: generateIV(),
                def: generateIV(),
                spatk: generateIV(),
                spdef: generateIV(),
                spd: generateIV()
            };
            const randomNature = POKEMON_NATURES[Math.floor(Math.random() * POKEMON_NATURES.length)];
            // Basic HP Calculation (Assuming Level 1 for this scenario, adjust formula per your game's math)
            // Standard Formula at Level 1: floor(0.01 * (2 * Base + IV + floor(0.25 * EV)) * Level) + Level + 10
            const level = 1;
            const calculatedMaxHp = Math.floor(0.01 * (2 * baseHp + ivs.hp) * level) + level + 10;
            // Step 2: Ensure we know if the player has room in their party
            const partyCount = await PokemonInstance_1.PokemonInstanceModel.countDocuments({ owner_id: playerId, is_in_party: true }).session(session);
            const is_in_party = partyCount < 6;
            const party_slot = is_in_party ? partyCount + 1 : undefined;
            // Step 3: Insert into PokemonInstance
            const newPokemon = new PokemonInstance_1.PokemonInstanceModel({
                owner_id: playerId,
                species_id: speciesId,
                level: level,
                nature: randomNature,
                current_hp: calculatedMaxHp,
                max_hp: calculatedMaxHp,
                is_in_party,
                party_slot // Only provided if it's placed in the party
            });
            const savedPokemon = await newPokemon.save({ session });
            // Step 4: Insert into PokemonStats
            const newStats = new PokemonStats_1.PokemonStatsModel({
                pokemon_instance_id: savedPokemon._id,
                ivs,
                evs: { hp: 0, atk: 0, def: 0, spatk: 0, spdef: 0, spd: 0 }
            });
            await newStats.save({ session });
            await session.commitTransaction();
            return savedPokemon;
        }
        catch (error) {
            await session.abortTransaction();
            console.error('Error catching Pokemon:', error);
            throw new Error('Failed to catch Pokemon');
        }
        finally {
            session.endSession();
        }
    }
    /**
     * 3. Secure Trading (Hub)
     * Uses a MongoDB Transaction to swap the owner_id of two Pokemon Instances simultaneously.
     */
    static async executeTrade(player1Id, player2Id, pokemonInstanceId1, pokemonInstanceId2) {
        const session = await mongoose_1.default.startSession();
        session.startTransaction();
        try {
            // Step 1: Verify ownership securely within the transaction
            const p1Pokemon = await PokemonInstance_1.PokemonInstanceModel.findOne({
                _id: pokemonInstanceId1,
                owner_id: player1Id
            }).session(session);
            const p2Pokemon = await PokemonInstance_1.PokemonInstanceModel.findOne({
                _id: pokemonInstanceId2,
                owner_id: player2Id
            }).session(session);
            if (!p1Pokemon)
                throw new Error('Player 1 does not own the specified Pokemon.');
            if (!p2Pokemon)
                throw new Error('Player 2 does not own the specified Pokemon.');
            // Step 2: Swap owners & remove from party (to prevent invalid party slots)
            p1Pokemon.owner_id = new mongoose_1.default.Types.ObjectId(player2Id.toString());
            p1Pokemon.is_in_party = false;
            p1Pokemon.party_slot = undefined;
            p2Pokemon.owner_id = new mongoose_1.default.Types.ObjectId(player1Id.toString());
            p2Pokemon.is_in_party = false;
            p2Pokemon.party_slot = undefined;
            // Step 3: Save changes atomatically
            await p1Pokemon.save({ session });
            await p2Pokemon.save({ session });
            await session.commitTransaction();
            return true;
        }
        catch (error) {
            await session.abortTransaction();
            console.error('Error executing trade:', error);
            throw error; // Re-throw to inform client of validation failure
        }
        finally {
            session.endSession();
        }
    }
    /**
     * 4. Boss Gating System (Wilderness)
     * Returns true if requiredBossId is in the player's beaten_bosses array.
     */
    static async checkCanEnterZone(playerId, requiredBossId) {
        try {
            // Checking if the boss array contains the required Boss ID.
            // We can do this efficiently using a MongoDB query rather than fetching the whole document.
            const player = await Player_1.PlayerModel.exists({
                _id: playerId,
                beaten_bosses: requiredBossId
            });
            return player !== null;
        }
        catch (error) {
            console.error('Error checking zone requirements:', error);
            return false; // Safely block entry on error
        }
    }
    /**
     * 5. Defeat Boss
     * Pushes the bossId into the player's beaten_bosses array.
     */
    static async onBossDefeated(playerId, bossId) {
        try {
            // Find the player and $addToSet ensures we don't duplicate the boss entry
            const result = await Player_1.PlayerModel.findByIdAndUpdate(playerId, { $addToSet: { beaten_bosses: bossId } }, { new: true });
            if (!result)
                throw new Error('Player not found');
        }
        catch (error) {
            console.error('Error recording boss defeat:', error);
            throw new Error('Failed to record boss defeat');
        }
    }
}
exports.GameService = GameService;
