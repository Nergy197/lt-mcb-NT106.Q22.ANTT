import mongoose, { Document, Schema, Types } from 'mongoose';

export interface IStatBlock {
  hp: number;
  atk: number;
  def: number;
  spatk: number;
  spdef: number;
  spd: number;
}

export interface IPokemonStats extends Document {
  pokemon_instance_id: Types.ObjectId;
  ivs: IStatBlock;
  evs: IStatBlock;
}

const StatBlockSchema = new Schema({
  hp: { type: Number, required: true, min: 0, max: 255 },
  atk: { type: Number, required: true, min: 0, max: 255 },
  def: { type: Number, required: true, min: 0, max: 255 },
  spatk: { type: Number, required: true, min: 0, max: 255 },
  spdef: { type: Number, required: true, min: 0, max: 255 },
  spd: { type: Number, required: true, min: 0, max: 255 }
}, { _id: false });

const PokemonStatsSchema: Schema = new Schema({
  pokemon_instance_id: { type: Schema.Types.ObjectId, ref: 'PokemonInstance', required: true, unique: true },
  ivs: { type: StatBlockSchema, required: true },
  evs: { type: StatBlockSchema, required: true }
});

export const PokemonStatsModel = mongoose.model<IPokemonStats>('PokemonStats', PokemonStatsSchema);
