import { Schema, type, MapSchema } from "@colyseus/schema";

export class PokemonInState extends Schema {
  @type("string") id: string = "";
  @type("number") species_id: number = 0;
  @type("number") level: number = 1;
  @type("number") hp: number = 0;
  @type("number") maxHp: number = 0;
}

export class PlayerInState extends Schema {
  @type("string") id: string = "";
  @type("string") name: string = "Trainer";
  @type("number") x: number = 0;
  @type("number") y: number = 0;
  @type("number") z: number = 0;
  @type({ map: PokemonInState }) party = new MapSchema<PokemonInState>();
}

export class GameState extends Schema {
  @type({ map: PlayerInState }) players = new MapSchema<PlayerInState>();
}
