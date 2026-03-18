import mongoose, { Document, Schema, Types } from 'mongoose';

export interface IPosition {
  x: number;
  y: number;
  z: number;
}

export interface IPlayer extends Document {
  account_id: Types.ObjectId;
  name: string;
  money: number;
  current_map: string;
  position: IPosition;
  beaten_bosses: string[];
}

const PlayerSchema: Schema = new Schema({
  account_id: { type: Schema.Types.ObjectId, ref: 'Account', required: true, index: true },
  name: { type: String, required: true, unique: true },
  money: { type: Number, default: 0 },
  current_map: { type: String, default: 'PalletTown' },
  position: {
    x: { type: Number, default: 0 },
    y: { type: Number, default: 0 },
    z: { type: Number, default: 0 }
  },
  beaten_bosses: { type: [String], default: [] }
});

export const PlayerModel = mongoose.model<IPlayer>('Player', PlayerSchema);
