export const modelLicencesIds = { root: 'settings-licences' } as const;

export const licenceEntryId = (model: string) => `settings-licences-${model}`;
export const licenceLinkId = (model: string) => `settings-licences-${model}-link`;
