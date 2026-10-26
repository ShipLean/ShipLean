import type { NewProjectForm } from './model';

export interface FormProps {
  form: NewProjectForm;
  patch: (change: Partial<NewProjectForm>) => void;
}
