import mongoose, { Document, Schema, Types } from 'mongoose';

export interface IPokemonInstance extends Document {
  owner_id: Types.ObjectId;
  species_id: number;
  nickname: string;
  level: number;
  exp: number;
  nature: string;
  current_hp: number;
  max_hp: number;
  status_condition: string;
  is_in_party: boolean;
  party_slot?: number;
}

const PokemonInstanceSchema: Schema = new Schema({
  owner_id: { type: Schema.Types.ObjectId, ref: 'Player', required: true, index: true },
  species_id: { type: Number, required: true },
  nickname: { type: String, default: '' },
  level: { type: Number, default: 1 },
  exp: { type: Number, default: 0 },
  nature: { type: String, required: true },
  current_hp: { type: Number, required: true },
  max_hp: { type: Number, required: true },
  status_condition: { type: String, default: 'NONE' },
  is_in_party: { type: Boolean, default: false },
  party_slot: { type: Number, min: 1, max: 6 }  // Only required/validated if is_in_party is true
});

// Adding a compound index to quickly find a player's party
PokemonInstanceSchema.index({ owner_id: 1, is_in_party: 1 });

export const PokemonInstanceModel = mongoose.model<IPokemonInstance>('PokemonInstance', PokemonInstanceSchema);
