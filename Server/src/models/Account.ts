import mongoose, { Document, Schema } from 'mongoose';

export interface IAccount extends Document {
  username: string;
  password_hash: string;
  email: string;
  created_at: Date;
}

const AccountSchema: Schema = new Schema({
  username: { type: String, required: true, unique: true },
  password_hash: { type: String, required: true },
  email: { type: String, required: true, unique: true },
  created_at: { type: Date, default: Date.now }
});

export const AccountModel = mongoose.model<IAccount>('Account', AccountSchema);
