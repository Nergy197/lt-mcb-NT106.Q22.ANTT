import fetch from 'node-fetch';

export interface IPokemonBaseData {
  id: number;
  name: string;
  base_hp: number;
  base_atk: number;
  base_def: number;
  base_spatk: number;
  base_spdef: number;
  base_spd: number;
}

/**
 * Service to handle static game data from PokeAPI.
 * In a production MMORPG, this data should be cached in memory or a fast Redis layer
 * to avoid expensive network calls during gameplay logic.
 */
export class PokemonDataService {
  private static cache: Map<number, IPokemonBaseData> = new Map();

  /**
   * Fetches basic data for a specific Pokemon species from PokeAPI.
   */
  public static async getPokemonData(speciesId: number): Promise<IPokemonBaseData> {
    // 1. Check if we already have this in memory
    if (this.cache.has(speciesId)) {
      return this.cache.get(speciesId)!;
    }

    try {
      console.log(`[StaticData] Fetching PokeAPI data for Species #${speciesId}...`);
      const response = await fetch(`https://pokeapi.co/api/v2/pokemon/${speciesId}`);
      
      if (!response.ok) {
        throw new Error(`Failed to fetch from PokeAPI: ${response.statusText}`);
      }

      const rawData: any = await response.json();

      // Mapping PokeAPI stats to our format
      const stats = rawData.stats;
      const getStat = (name: string) => stats.find((s: any) => s.stat.name === name)?.base_stat || 0;

      const baseData: IPokemonBaseData = {
        id: rawData.id,
        name: rawData.name,
        base_hp: getStat('hp'),
        base_atk: getStat('attack'),
        base_def: getStat('defense'),
        base_spatk: getStat('special-attack'),
        base_spdef: getStat('special-defense'),
        base_spd: getStat('speed')
      };

      // 2. Save to cache
      this.cache.set(speciesId, baseData);
      return baseData;

    } catch (error) {
      console.error(`[StaticData] Error fetching species #${speciesId}:`, error);
      throw new Error(`Could not load master data for Pokemon #${speciesId}`);
    }
  }

  /**
   * Pre-loads common Pokemon to memory (e.g., starters)
   */
  public static async preWarm(ids: number[]) {
    for (const id of ids) {
      await this.getPokemonData(id);
    }
    console.log(`[StaticData] Pre-warmed ${ids.length} Pokemon into memory.`);
  }
}
