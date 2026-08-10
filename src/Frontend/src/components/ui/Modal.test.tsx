import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from './Modal';

const renderModal = (onClose = vi.fn()) => {
  render(
    <Modal open title="Novo lançamento" onClose={onClose}>
      <button type="button">Salvar</button>
    </Modal>,
  );
  return onClose;
};

describe('Modal', () => {
  it('is announced as a dialog with its title', () => {
    renderModal();
    expect(screen.getByRole('dialog', { name: 'Novo lançamento' })).toBeInTheDocument();
  });

  it('renders nothing while closed', () => {
    render(
      <Modal open={false} title="Novo lançamento" onClose={vi.fn()}>
        <span>conteúdo</span>
      </Modal>,
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('closes on Escape', async () => {
    const onClose = renderModal();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('closes on a click outside the panel', async () => {
    const onClose = renderModal();
    await userEvent.click(screen.getByTestId('modal-backdrop'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('keeps the focus in the field when the page re-renders', async () => {
    // A background refetch re-renders the page, and the caller builds `onClose`
    // inline, so the dialog gets a new identity for it. Focusing the panel again
    // there used to pull the caret out of the field mid-typing.
    const panel = (onClose: () => void) => (
      <Modal open title="Novo lançamento" onClose={onClose}>
        <input aria-label="Valor" />
      </Modal>
    );

    const { rerender } = render(panel(vi.fn()));
    const field = screen.getByLabelText('Valor');
    await userEvent.click(field);
    expect(field).toHaveFocus();

    const latestOnClose = vi.fn();
    rerender(panel(latestOnClose));

    expect(field).toHaveFocus();

    // The listener still reaches the current callback, not the one it closed over.
    await userEvent.keyboard('{Escape}');
    expect(latestOnClose).toHaveBeenCalledOnce();
  });

  it('does not close on a click inside the panel', async () => {
    // Selecting text in a field and releasing outside used to close the dialog
    // and discard everything typed.
    const onClose = renderModal();
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));
    expect(onClose).not.toHaveBeenCalled();
  });
});
