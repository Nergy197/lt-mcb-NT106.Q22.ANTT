import mongoose, { Document, Schema, Types } from 'mongoose';

export interface IPokemonMoves extends Document {
  pokemon_instance_id: Types.ObjectId;
  move_id: number;
  slot: number;
  current_pp: number;
}

const PokemonMovesSchema: Schema = new Schema({
  pokemon_instance_id: { type: Schema.Types.ObjectId, ref: 'PokemonInstance', required: true, index: true },
  move_id: { type: Number, required: true },
  slot: { type: Number, required: true, min: 1, max: 4 },
  current_pp: { type: Number, required: true, min: 0 }
});

// A Pokemon cannot have the same slot duplicated
PokemonMovesSchema.index({ pokemon_instance_id: 1, slot: 1 }, { unique: true });

export const PokemonMovesModel = mongoose.model<IPokemonMoves>('PokemonMoves', PokemonMovesSchema);
